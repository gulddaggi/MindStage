using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RadarChartGraphic : Graphic
{
    [Header("Scale")]
    [Range(1, 100)] public float max = 100f;
    public float padding = 16f;

    [Header("Lines")]
    public float gridThickness = 2f;     // 격자/방사선
    public float valueThickness = 3f;    // 값 폴리라인
    public Color gridColor = new Color(0.6f, 0.6f, 0.6f, 0.9f);
    public Color myColor = new Color(0.2f, 1f, 0.2f, 1f);
    public Color peerColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("Labels")]
    public bool drawLabels = true;
    [Tooltip("방사선 끝에서 라벨이 놓일 반지름 배율(1 = 외곽 꼭짓점)")]
    public float labelRadiusFactor = 1.08f;
    public TMP_FontAsset labelFont;
    public int labelFontSize = 24;
    public Color labelColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [Tooltip("의사소통, 적응성, 팀워크, 직무 능력, 진실성 (시계방향)")]
    public string[] axisLabels = new[] { "의사소통", "적응성", "팀워크", "직무 능력", "진실성" };

    [Header("Data (length = 5)")]
    public float[] my = new float[5];
    public float[] peer = null;

    TMP_Text[] _labelRefs;

    bool _needLabelRecalc;

    public void SetData(float[] mine, float[] peerAvg)
    {
        my = mine; peer = peerAvg;
        SetVerticesDirty();
        SetLabelsDirty();
    }

    public void SetAxisLabels(string[] labels)
    {
        if (labels != null && labels.Length == 5)
        {
            axisLabels = (string[])labels.Clone();
            SetLabelsDirty();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureLabelObjects();
        SetLabelsDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        Vector2 c = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f - padding;
        if (radius <= 0f) { UpdateLabelPositions(); return; }

        const int N = 5;
        float StepRad(int i) => (90f - i * 360f / N) * Mathf.Deg2Rad;

        Vector2 PointAt(int i, float factor01)
        {
            float ang = StepRad(i);
            return c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (radius * factor01);
        }

        // 1) 격자 (100/75/50/25)
        float[] rings = { 1f, 0.75f, 0.5f, 0.25f };
        foreach (var f in rings)
        {
            var pts = new List<Vector2>(N);
            for (int i = 0; i < N; i++) pts.Add(PointAt(i, f));
            AddPolyline(vh, pts, true, gridThickness, gridColor);
        }

        // 2) 방사선 5개
        for (int i = 0; i < N; i++)
        {
            var a = c; var b = PointAt(i, 1f);
            AddPolyline(vh, new List<Vector2> { a, b }, false, gridThickness, gridColor);
        }

        // 3) 평균(빨강) / 내 점수(초록)
        if (peer != null && peer.Length == N)
        {
            var pts = new List<Vector2>(N);
            for (int i = 0; i < N; i++)
            {
                float t = Normalize100To5FactorFeer(peer[i]);
                pts.Add(PointAt(i, t));
            }
            AddPolyline(vh, pts, true, valueThickness, peerColor);
        }


        if (my != null && my.Length == N)
        {
            var pts = new List<Vector2>(N);
            for (int i = 0; i < N; i++)
            {
                float t = Normalize100To5Factor(my[i]); 
                pts.Add(PointAt(i, t));
            }
            AddPolyline(vh, pts, true, valueThickness, myColor);
        }

        // 라벨 위치 업데이트
        UpdateLabelPositions();

        _needLabelRecalc = true;
    }

    // === helpers ===
    static void AddPolyline(VertexHelper vh, IList<Vector2> pts, bool closed, float thickness, Color col)
    {
        if (pts == null || pts.Count < 2 || thickness <= 0f) return;

        int count = closed ? pts.Count : pts.Count - 1;
        float half = thickness * 0.5f;

        for (int i = 0; i < count; i++)
        {
            var p0 = pts[i];
            var p1 = pts[(i + 1) % pts.Count];

            Vector2 dir = (p1 - p0);
            if (dir.sqrMagnitude <= 1e-6f) continue;
            dir.Normalize();
            Vector2 n = new Vector2(-dir.y, dir.x);

            Vector2 v0 = p0 - n * half;
            Vector2 v1 = p0 + n * half;
            Vector2 v2 = p1 + n * half;
            Vector2 v3 = p1 - n * half;

            int baseIndex = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert; v.color = col;

            v.position = v0; vh.AddVert(v);
            v.position = v1; vh.AddVert(v);
            v.position = v2; vh.AddVert(v);
            v.position = v3; vh.AddVert(v);

            vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
            vh.AddTriangle(baseIndex + 0, baseIndex + 2, baseIndex + 3);
        }
    }

    void EnsureLabelObjects()
    {
        if (!drawLabels) return;
        if (_labelRefs != null && _labelRefs.Length == 5) return;

        _labelRefs = new TMP_Text[5];
        for (int i = 0; i < 5; i++)
        {
            var child = transform.Find($"Label{i}")?.gameObject;
            if (child == null)
            {
                child = new GameObject($"Label{i}", typeof(RectTransform), typeof(TextMeshProUGUI));
                child.transform.SetParent(transform, false);
            }
            var rt = (RectTransform)child.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var t = child.GetComponent<TextMeshProUGUI>() ?? child.AddComponent<TextMeshProUGUI>();
            t.text = (axisLabels != null && axisLabels.Length > i) ? axisLabels[i] : $"L{i + 1}";
            t.font = labelFont ?? t.font;
            t.fontSize = labelFontSize;
            t.color = labelColor;
            t.enableWordWrapping = false;
            t.raycastTarget = false;

            _labelRefs[i] = t;
        }
    }

    void SetLabelsDirty()
    {
        if (!drawLabels || _labelRefs == null) return;
        for (int i = 0; i < _labelRefs.Length; i++)
        {
            if (_labelRefs[i] == null) continue;
            _labelRefs[i].text = (axisLabels != null && axisLabels.Length > i) ? axisLabels[i] : $"L{i + 1}";
            _labelRefs[i].fontSize = labelFontSize;
            _labelRefs[i].color = labelColor;
            _labelRefs[i].rectTransform.sizeDelta = new Vector2(100f, 50f);
            if (labelFont) _labelRefs[i].font = labelFont;
        }
        SetVerticesDirty();
    }

    void UpdateLabelPositions()
    {
        if (!drawLabels || _labelRefs == null) return;

        Rect rect = rectTransform.rect;
        Vector2 c = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f - padding;

        const int N = 5;
        float StepRad(int i) => (90f - i * 360f / N) * Mathf.Deg2Rad;

        for (int i = 0; i < N; i++)
        {
            var t = _labelRefs[i];
            if (t == null) continue;

            float ang = StepRad(i);
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

            // 피벗/정렬은 그대로
            var rt = (RectTransform)t.transform;
            if (Mathf.Abs(dir.x) < 0.25f) { rt.pivot = new Vector2(0.5f, dir.y > 0 ? 0f : 1f); t.alignment = TextAlignmentOptions.Center; }
            else if (dir.x > 0) { rt.pivot = new Vector2(0f, 0.5f); t.alignment = TextAlignmentOptions.Left; }
            else { rt.pivot = new Vector2(1f, 0.5f); t.alignment = TextAlignmentOptions.Right; }

            // 로컬(앵커=Center) 기준 오프셋만 사용
            rt.anchoredPosition = dir * (radius * labelRadiusFactor);
        }
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
        _needLabelRecalc = true;
    }

    void LateUpdate()
    {
        if (_needLabelRecalc)
        {
            UpdateLabelPositions();
            _needLabelRecalc = false;
        }
    }

    public void SetScores(float[] my, float[] avg)
    {
        // 기존 새 API에 위임
        SetData(my, avg);
    }

    float Normalize100To5Factor(float raw)
    {
        // 0점 이하면 1점으로 강제
        if (raw <= 0f) return 1f / 5f;

        // 0~max(보통 100) 사이로 클램프
        float clamped = Mathf.Clamp(raw, 0f, Mathf.Max(1f, max));

        // 0~1 비율로 환산 (예: 50/100 = 0.5)
        float t01 = clamped / Mathf.Max(1f, max);

        // 0~1 비율을 1~5 점수로 매핑
        float score5 = 1f + t01 * 4f; // 0 → 1점, 100 → 5점

        // 다시 0~1 비율로 (1점=0.2, 5점=1.0)
        return score5 / 5f;
    }

    float Normalize100To5FactorFeer(float raw)
    {
        // 0~max(보통 100) 사이로 클램프
        float clamped = Mathf.Clamp(raw, 0f, Mathf.Max(1f, max));

        // 0~1 비율로 환산 (예: 50/100 = 0.5)
        float t01 = clamped / Mathf.Max(1f, max);

        // 0~1 비율을 1~5 점수로 매핑
        float score5 = t01 * 5f; // 0 → 1점, 100 → 5점

        // 다시 0~1 비율로 (1점=0.2, 5점=1.0)
        return score5 / 5f;
    }
}