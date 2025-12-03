using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct HeartPoint
{
    public float t;    // time (sample index)
    public float bpm;  // beats per minute
    public HeartPoint(float t, float bpm) { this.t = t; this.bpm = bpm; }
}

[System.Serializable]
public struct QuestionRange
{
    /// <summary>[0,1] 정규화된 시작 X 좌표</summary>
    public float start01;
    /// <summary>[0,1] 정규화된 끝 X 좌표</summary>
    public float end01;
    /// <summary>질문 인덱스(1~5 등) - 색 분기 등에 쓰고 싶으면 사용</summary>
    public int index;

    public QuestionRange(float start01, float end01, int index = 0)
    {
        this.start01 = start01;
        this.end01 = end01;
        this.index = index;
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public class HeartRateChartGraphic : Graphic
{
    [Header("Value Range")]
    public int baseline = 90;
    public int yMin = 60, yMax = 140;     // 필요 시 서버에서 같이 전달 가능
    public float lineWidth = 2f;

    [Header("Grid")]
    public bool drawOuterFrame = true;    // 위/아래 경계 가로선
    public bool drawVerticalGrid = true;  // 세로선
    [Range(2, 64)]
    public int verticalDivisions = 12;    // 세로선 개수(등분 수)
    public float gridLineWidth = 1f;
    public Color gridColor = new Color(1f, 1f, 1f, 0.25f);
    public Color baselineColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Question Grid")]
    public Color questionBoundaryColor = Color.yellow;

    [Header("Fill / Line")]
    public Color aboveColor = new Color(1f, 0f, 0f, 0.25f);   // 기준선 위(빨강)
    public Color belowColor = new Color(0f, 1f, 0f, 0.25f);   // 기준선 아래(초록)
    public Color lineColor = Color.white;

    [Header("Baseline Label")]
    public TMP_Text baselineLabel;                 // "평균 심박수\n95" 같은 텍스트
    public string baselineLabelFormat = "평균 심박수\n{0}";
    public float baselineLabelXOffset = -40f;

    [Header("Smoothing")]
    [Tooltip("그래프를 부드럽게 만들기 위한 이동 평균 윈도우 크기 (1이면 원본 그대로 사용).")]
    [Range(1, 60)]
    public int movingAverageWindow = 5;

    [Tooltip("보고서용 그래프에서 사용할 최대 샘플 개수. 값이 작을수록 더 완만한 곡선이 됩니다.")]
    [Range(8, 256)]
    public int maxSampleCount = 24;

    [Header("Question Ranges")]
    public bool drawQuestionRanges = true;
    public Color questionRangeColor = new Color(1f, 1f, 1f, 0.08f);
    public List<QuestionRange> questionRanges = new();

    public List<Vector2> samples = new(); // x:[0,1], y=bpm

    private readonly List<HeartPoint> _tmp = new List<HeartPoint>(1024);

    [Header("Question Labels")]
    public TMP_Text[] questionLabels;
    public string questionLabelFormat = "{0}번";

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (samples == null || samples.Count < 2) return;

        Rect r = GetPixelAdjustedRect();

        // 좌표 변환: [0,1] x, 실제 bpm → 로컬 픽셀 좌표
        Vector2 ToLocal(Vector2 xy)
        {
            float x = Mathf.Lerp(r.xMin, r.xMax, xy.x);
            float yy = Mathf.InverseLerp(yMin, yMax, xy.y);
            float y = Mathf.Lerp(r.yMin, r.yMax, yy);
            return new Vector2(x, y);
        }

        float yBase = Mathf.Lerp(r.yMin, r.yMax, Mathf.InverseLerp(yMin, yMax, baseline));

        // ---------- 0) 그리드/프레임(맨 뒤) ----------
        if (drawOuterFrame)
        {
            // 아래 경계선
            AddHLine(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), gridColor, gridLineWidth);
            // 위 경계선
            AddHLine(vh, new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), gridColor, gridLineWidth);
        }

        if (drawVerticalGrid)
        {
            // 1) 기본 등간격 그리드 (예전 동작 그대로)
            int div = Mathf.Clamp(verticalDivisions, 2, 64);
            for (int i = 0; i <= div; i++)
            {
                float t = i / (float)div;
                float x = Mathf.Lerp(r.xMin, r.xMax, t);
                AddThickLine(vh,
                    new Vector2(x, r.yMin),
                    new Vector2(x, r.yMax),
                    gridLineWidth,
                    gridColor);
            }

            // 2) 질문 구간 경계선 (있을 때만, 흰색으로 오버레이)
            if (questionRanges != null && questionRanges.Count > 0)
            {
                var xs01 = BuildQuestionGridPoints01(); // 0~1 사이 정렬된 경계들

                for (int i = 0; i < xs01.Count; i++)
                {
                    float t = xs01[i];
                    float x = Mathf.Lerp(r.xMin, r.xMax, t);
                    AddThickLine(vh,
                        new Vector2(x, r.yMin),
                        new Vector2(x, r.yMax),
                        gridLineWidth * 2,
                        questionBoundaryColor);   // ← 흰색
                }
            }
        }


        // ---------- 0.5) 질문 구간 밴드 ----------
        if (drawQuestionRanges && questionRanges != null && questionRanges.Count > 0)
        {
            foreach (var qr in questionRanges)
            {
                float x0 = Mathf.Lerp(r.xMin, r.xMax, Mathf.Clamp01(qr.start01));
                float x1 = Mathf.Lerp(r.xMin, r.xMax, Mathf.Clamp01(qr.end01));
                if (x1 <= x0) continue;

                AddRect(vh,
                    new Vector2(x0, r.yMin),
                    new Vector2(x1, r.yMax),
                    questionRangeColor);
            }
        }

        // ---------- 1) 기준선 ----------
        AddHLine(vh,
            new Vector2(r.xMin, yBase),
            new Vector2(r.xMax, yBase),
            baselineColor,
            gridLineWidth);

        // ---------- 2) 영역 채우기(세그먼트 단위로 분할, 교차점 보정) ----------
        for (int i = 0; i < samples.Count - 1; i++)
        {
            var a = samples[i];
            var b = samples[i + 1];
            var la = ToLocal(a);
            var lb = ToLocal(b);

            // 교차 여부
            bool cross = (a.y - baseline) * (b.y - baseline) < 0f;
            if (!cross)
            {
                bool above = (a.y >= baseline || b.y >= baseline);
                AddTrapezoidToBaseline(
                    vh,
                    la,
                    lb,
                    yBase,
                    above ? aboveColor : belowColor);
            }
            else
            {
                // 교차점 계산
                float t = Mathf.InverseLerp(a.y, b.y, baseline);
                Vector2 mid = Vector2.Lerp(la, lb, t);
                bool aAbove = a.y >= baseline;
                AddTrapezoidToBaseline(
                    vh,
                    la,
                    mid,
                    yBase,
                    aAbove ? aboveColor : belowColor);

                bool bAbove = b.y >= baseline;
                AddTrapezoidToBaseline(
                    vh,
                    mid,
                    lb,
                    yBase,
                    bAbove ? aboveColor : belowColor);
            }
        }

        // ---------- 3) 라인(얇은 폴리라인) ----------
        for (int i = 0; i < samples.Count - 1; i++)
        {
            var p0 = ToLocal(samples[i]);
            var p1 = ToLocal(samples[i + 1]);
            AddThickLine(vh, p0, p1, lineWidth, lineColor);
        }
    }

    /// <summary>
    /// 질문 구간(start01/end01)들을 기반으로 0~1 사이 세로선 위치들을 반환.
    /// 항상 0과 1을 포함하며, 중복/거의 같은 값은 제거한다.
    /// </summary>
    List<float> BuildQuestionGridPoints01()
    {
        var points = new List<float>();

        // 기본 좌우 경계
        points.Add(0f);
        points.Add(1f);

        if (questionRanges != null)
        {
            for (int i = 0; i < questionRanges.Count; i++)
            {
                var qr = questionRanges[i];
                points.Add(Mathf.Clamp01(qr.start01));
                points.Add(Mathf.Clamp01(qr.end01));
            }
        }

        // 정렬 + 중복 제거
        points.Sort();
        const float eps = 0.0001f;
        var uniq = new List<float>();
        for (int i = 0; i < points.Count; i++)
        {
            float v = points[i];
            if (uniq.Count == 0 || Mathf.Abs(v - uniq[uniq.Count - 1]) > eps)
                uniq.Add(v);
        }

        return uniq;
    }


    void AddThickLine(VertexHelper vh, Vector2 a, Vector2 b, float w, Color col)
    {
        var dir = (b - a).normalized;
        var n = new Vector2(-dir.y, dir.x) * (w * 0.5f);
        var v0 = a - n; var v1 = a + n; var v2 = b + n; var v3 = b - n;
        int idx = vh.currentVertCount;
        AddVert(vh, v0, col); AddVert(vh, v1, col); AddVert(vh, v2, col); AddVert(vh, v3, col);
        vh.AddTriangle(idx, idx + 1, idx + 2); vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    void AddHLine(VertexHelper vh, Vector2 a, Vector2 b, Color col, float w)
    {
        AddThickLine(vh, a, b, w, col);
    }

    void AddTrapezoidToBaseline(VertexHelper vh, Vector2 a, Vector2 b, float yBase, Color fill)
    {
        // a, b 와 기준선으로 이루어진 사다리꼴
        var aB = new Vector2(a.x, yBase);
        var bB = new Vector2(b.x, yBase);
        int idx = vh.currentVertCount;
        AddVert(vh, aB, fill); AddVert(vh, a, fill); AddVert(vh, b, fill); AddVert(vh, bB, fill);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    void AddRect(VertexHelper vh, Vector2 bottomLeft, Vector2 topRight, Color fill)
    {
        int idx = vh.currentVertCount;
        AddVert(vh, new Vector2(bottomLeft.x, bottomLeft.y), fill);
        AddVert(vh, new Vector2(bottomLeft.x, topRight.y), fill);
        AddVert(vh, new Vector2(topRight.x, topRight.y), fill);
        AddVert(vh, new Vector2(topRight.x, bottomLeft.y), fill);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    static void AddVert(VertexHelper vh, Vector2 p, Color c)
    {
        var v = UIVertex.simpleVert;
        v.position = p;
        v.color = c;
        vh.AddVert(v);
    }

    /// <summary>
    /// 샘플 포인트(t,bpm) → [0,1] 정규화 x 좌표 + bpm으로 변환
    /// </summary>
    public void SetData(int baselineBpm, IList<HeartPoint> pts, int yMin = 60, int yMax = 140)
    {
        baseline = baselineBpm;
        this.yMin = yMin;
        this.yMax = yMax;

        samples.Clear();
        if (pts != null && pts.Count > 1)
        {
            float t0 = pts[0].t, t1 = pts[pts.Count - 1].t;
            float span = Mathf.Max(0.0001f, t1 - t0);
            for (int i = 0; i < pts.Count; i++)
            {
                float x = (pts[i].t - t0) / span;
                samples.Add(new Vector2(x, pts[i].bpm));
            }
        }

        UpdateBaselineLabel();
        SetVerticesDirty();
    }

    /// <summary>
    /// int BPM 리스트를 그대로 줬을 때, 이동 평균 + 다운샘플링을 적용한 뒤 SetData 호출.
    /// </summary>
    public void SetValues(IList<int> beats, int baseline = 90, int yMin = 60, int yMax = 140)
    {
        _tmp.Clear();
        if (beats != null && beats.Count > 0)
        {
            BuildSmoothedPoints(beats, _tmp);
        }

        SetData(baseline, _tmp, yMin, yMax);
    }

    /// <summary>
    /// 원본 BPM 리스트에 이동 평균을 적용하고, maxSampleCount 개 이하로 다운샘플링해서
    /// 보다 완만한 곡선을 만들기 위한 포인트 시퀀스를 생성.
    /// </summary>
    void BuildSmoothedPoints(IList<int> beats, List<HeartPoint> outPoints)
    {
        int n = beats.Count;
        if (n <= 0) return;

        int win = Mathf.Max(1, movingAverageWindow);

        // 1) 이동 평균 (노이즈 제거)
        float[] smoothed = new float[n];
        if (win <= 1)
        {
            for (int i = 0; i < n; i++)
                smoothed[i] = beats[i];
        }
        else
        {
            int half = win / 2;
            for (int i = 0; i < n; i++)
            {
                int start = Mathf.Max(0, i - half);
                int end = Mathf.Min(n - 1, i + half);
                float sum = 0f;
                int cnt = 0;
                for (int j = start; j <= end; j++)
                {
                    sum += beats[j];
                    cnt++;
                }

                smoothed[i] = (cnt > 0) ? (sum / cnt) : beats[i];
            }
        }

        // 2) 다운샘플링 (너무 촘촘한 포인트를 줄여서 경사를 완만하게)
        int target = Mathf.Clamp(maxSampleCount, 2, n);
        int windowDown = Mathf.CeilToInt(n / (float)target);

        for (int start = 0; start < n; start += windowDown)
        {
            int end = Mathf.Min(n, start + windowDown);
            if (end <= start)
                break;

            float sum = 0f;
            int cnt = 0;
            for (int i = start; i < end; i++)
            {
                sum += smoothed[i];
                cnt++;
            }

            float avg = sum / Mathf.Max(1, cnt);
            float midT = (start + (end - 1)) * 0.5f;   // 윈도우 중앙 인덱스를 시간으로 사용

            outPoints.Add(new HeartPoint(midT, avg));
        }
    }

    public void SetQuestionRanges(IList<QuestionRange> ranges)
    {
        questionRanges.Clear();
        if (ranges != null)
            questionRanges.AddRange(ranges);

        UpdateQuestionLabels();   // 라벨도 같이 갱신
        SetVerticesDirty();
    }

    void UpdateBaselineLabel()
    {
        if (baselineLabel == null) return;

        var rt = (RectTransform)transform;
        var lrt = baselineLabel.rectTransform;

        // 현재 Rect 안에서 baseline이 차지하는 정규화 비율
        float ny = Mathf.InverseLerp(yMin, yMax, baseline);

        // Rect(local) 좌표로 변환
        var rect = rt.rect;
        float y = Mathf.Lerp(rect.yMin, rect.yMax, ny);

        // 라벨의 anchoredPosition을 baseline 높이에 맞춰줌
        var pos = lrt.anchoredPosition;
        pos.y = y;
        // 왼쪽 끝 + 오프셋 (그래프 영역보다 살짝 더 왼쪽으로 빼고 싶으면 baselineLabelXOffset 음수)
        pos.x = rect.xMin + baselineLabelXOffset;
        lrt.anchoredPosition = pos;

        // 텍스트 세팅 ("평균 심박수\n95" 형태)
        baselineLabel.richText = true;
        baselineLabel.text = string.Format(baselineLabelFormat, baseline);
    }

    void UpdateQuestionLabels()
    {
        if (questionLabels == null || questionLabels.Length == 0) return;

        // 전부 숨김
        for (int i = 0; i < questionLabels.Length; i++)
        {
            if (questionLabels[i] != null)
                questionLabels[i].gameObject.SetActive(false);
        }

        if (questionRanges == null || questionRanges.Count == 0) return;

        if (questionRanges.Count < 5) return;

        var chartRt = (RectTransform)transform;
        var rect = chartRt.rect;
        float width = rect.width;

        int labelCount = Mathf.Min(questionLabels.Length, questionRanges.Count);
        for (int i = 0; i < labelCount; i++)
        {
            var lbl = questionLabels[i];
            if (lbl == null) continue;

            var qr = questionRanges[i];
            float mid01 = Mathf.Lerp(qr.start01, qr.end01, 0.5f);

            var lrt = lbl.rectTransform;

            // 수평 기준은 항상 "가운데" 로 고정해서 좌표계 맞추기
            lrt.anchorMin = new Vector2(0.5f, lrt.anchorMin.y);
            lrt.anchorMax = new Vector2(0.5f, lrt.anchorMax.y);
            lrt.pivot = new Vector2(0.5f, lrt.pivot.y);

            // mid01(0~1) -> [-width/2, +width/2] 로 변환
            float localX = (mid01 - 0.5f) * width;

            var pos = lrt.anchoredPosition;
            pos.x = localX;      // X 는 스크립트에서, Y 는 에디터에서 설정
            lrt.anchoredPosition = pos;

            int labelIndex = (qr.index > 0) ? qr.index : (i + 1);
            lbl.text = string.Format(questionLabelFormat, labelIndex);
            lbl.gameObject.SetActive(true);
        }
    }


    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        UpdateBaselineLabel();
        UpdateQuestionLabels();
    }
}
