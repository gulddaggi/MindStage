using App.Core;
using App.Infra;
using App.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ReportDetailController : MonoBehaviour
{
    [Header("Texts")]
    public TMP_Text tCompany;
    public TMP_Text tJob;
    public TMP_Text tDate;
    public TMP_Text tSession;
    public TMP_Text tSummary;

    [Header("Buttons")]
    public Button btnSavePdf;
    public Button btnBack;

    [Header("CaptureRoot")]
    public RectTransform captureRoot; // UI 패널 전체(요약+헤더 포함)

    [Header("Footer")]
    public GameObject footer;

    IReportService _reports;
    ReportDetailDto _data;

    [Header("Overview")]
    public RadarChartGraphic radar;
    public HeartRateChartGraphic heartChart;

    [Header("Tabs")]
    public Button btnAll;
    public Button[] btnQuestions;      // 크기=5
    public GameObject overviewPanel;
    public Transform questionsRoot;    // QuestionPage 프리팹 붙일 부모
    public GameObject questionPagePrefab;

    [Header("Prefabs")]
    public GameObject qnaItemPrefab;

    [Header("PDF Export")]
    public Canvas pdfCanvas;
    public Transform pdfPagesRoot;
    public PdfOverviewPageBinder pdfOverviewPagePrefab;
    public PdfQuestionPageBinder pdfQuestionPagePrefab;
    public GameObject pdfQnaItemPrefab;

    private List<QuestionRange> _questionRanges;

    private List<QuestionHrAvgDto> _questionHrData;

    [Serializable]
    class QuestionHrAvgDto
    {
        public int questionId;
        public string startAt;
        public string endAt;
        public int avgBpm;
    }

    [Serializable]
    class QuestionHrAvgEnvelope
    {
        public bool success;
        public string message;
        public int code;
        public List<QuestionHrAvgDto> data;
    }

    async void Start()
    {
        _reports = Services.Resolve<IReportService>();

        // 목록에서 넘겨준 메타
        tCompany.text = "회사 | " + PlayerPrefs.GetString("report.detail.company", "-");
        tJob.text = "직무 | " + PlayerPrefs.GetString("report.detail.job", "-");
        tDate.text = "총 " + PlayerPrefs.GetString("report.detail.datetime", "-") + "분간 진행";

        var id = PlayerPrefs.GetString("report.detail.id", null);
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("report.detail.id 가 없습니다.");
            return;
        }

        if (btnBack) btnBack.onClick.AddListener(() => _ = App.Infra.SceneLoader.LoadSingleAsync(SceneIds.ResultsList));
        if (btnSavePdf) btnSavePdf.onClick.AddListener(() => StartCoroutine(SavePdfWithPdfCanvasCoroutine()));

        try
        {
            _data = await _reports.GetDetailAsync(id);

            List<QuestionHrAvgDto> qHr = null;
            try
            {
                qHr = await LoadQuestionHrAvgAsync(id);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"LoadQuestionHrAvgAsync failed: {ex}");
            }

            _questionHrData = qHr;

            tSummary.richText = true;
            tSummary.text = string.IsNullOrEmpty(_data.comment) ? "(요약 없음)" : MdToTmp(_data.comment);

            // 레이더 차트 (키 순서: Communication, Adaptability, Teamwork_Leadership, Job_Competency, Integrity)
            var mine = ToRadarArray(_data.myScores);
            var avg = ToRadarArray(_data.averageScores);   // 서버가 {} 줄 수 있으므로 null/0 허용
            if (radar != null && mine != null) radar.SetScores(mine, avg);

            // 심박 그래프
            if (heartChart != null && _data.heartBeats != null && _data.heartBeats.Count >= 2)
            {
                // 1) 서버에서 온 샘플들을 bpm 리스트로 변환
                var beats = new List<int>(_data.heartBeats.Count);
                foreach (var hb in _data.heartBeats)
                {
                    beats.Add(hb.bpm);
                }

                if (beats.Count >= 2)
                {
                    int count = beats.Count;

                    float sum = 0f;
                    int min = int.MaxValue;
                    int max = int.MinValue;

                    for (int i = 0; i < count; i++)
                    {
                        int v = beats[i];
                        sum += v;
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }

                    int baselineBpm = Mathf.RoundToInt(sum / count);

                    float dLower = Mathf.Abs(baselineBpm - min);
                    float dUpper = Mathf.Abs(max - baselineBpm);
                    float d = Mathf.Max(dLower, dUpper);

                    int pad = Mathf.Max(2, Mathf.CeilToInt(d * 0.1f));
                    int halfRange = Mathf.CeilToInt(d + pad);

                    int yMin = Mathf.Max(40, baselineBpm - halfRange);
                    int yMax = baselineBpm + halfRange;

                    // 메인 심박수 곡선
                    heartChart.SetValues(beats, baselineBpm, yMin, yMax);

                    _questionRanges = BuildQuestionRanges(qHr, _data.heartBeats);

                    if (_questionRanges != null && _questionRanges.Count > 0)
                    {
                        heartChart.SetQuestionRanges(_questionRanges);
                    }
                    else
                    {
                        // 워치를 안 썼거나, 데이터가 부족한 경우
                        _questionRanges = null;
                        heartChart.SetQuestionRanges(null);
                    }
                }
            }
            else
            {
                if (heartChart != null)
                    heartChart.gameObject.SetActive(false);
            }

            // Q&A
            BuildQuestionPages(_data.qnaList, _questionHrData);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            tSummary.text = "(데이터 로드 실패)";
        }

        ShowTab(-1);
    }

    List<QuestionRange> BuildQuestionRanges(
    List<QuestionHrAvgDto> avgList,
    List<HeartBeatSampleDto> beats)
    {
        var result = new List<QuestionRange>();
        if (avgList == null || avgList.Count == 0) return result;
        if (beats == null || beats.Count == 0) return result;

        // 1) 심박수 전체 시간 범위
        if (!TryGetBeatTimeRange(beats, out var beatStart, out var beatEnd))
            return result;

        double totalSec = (beatEnd - beatStart).TotalSeconds;
        if (totalSec <= 0.1) return result;

        // 2) 질문별 startAt 파싱
        var starts = new List<DateTime>();
        foreach (var item in avgList)
        {
            if (string.IsNullOrEmpty(item.startAt)) continue;
            if (!DateTime.TryParse(item.startAt, out var s)) continue;
            starts.Add(s);
        }
        if (starts.Count == 0) return result;

        // 시간순 정렬
        starts.Sort();

        // 거의 같은 시각(startAt) 은 하나로 취급
        var uniqueStarts = new List<DateTime>();
        foreach (var s in starts)
        {
            if (uniqueStarts.Count == 0)
            {
                uniqueStarts.Add(s);
            }
            else
            {
                var last = uniqueStarts[uniqueStarts.Count - 1];
                if (Math.Abs((s - last).TotalSeconds) > 0.01f)
                    uniqueStarts.Add(s);
            }
        }

        int uCount = uniqueStarts.Count;
        if (uCount == 0) return result;

        // 만들 구간 개수:
        //  - 유니크한 시작 시간이 5개 이하이면 그대로 개수만큼
        //  - 5개보다 많으면 앞에서 5개만 사용 (질문 5개)
        int maxSegments = 5;
        int segCount = Mathf.Min(maxSegments, uCount);

        for (int i = 0; i < segCount; i++)
        {
            var sTime = uniqueStarts[i];
            DateTime eTime;

            if (i < segCount - 1)
            {
                // 다음 시작 시각까지
                eTime = uniqueStarts[i + 1];
            }
            else
            {
                // 마지막 구간은 그래프 끝(마지막 심박수 시각)까지
                eTime = beatEnd;
            }

            if (eTime <= sTime) continue;

            float x0 = (float)((sTime - beatStart).TotalSeconds / totalSec);
            float x1 = (float)((eTime - beatStart).TotalSeconds / totalSec);

            result.Add(new QuestionRange(Mathf.Clamp01(x0), Mathf.Clamp01(x1), i + 1));
        }

        return result;
    }



    bool TryGetBeatTimeRange(List<HeartBeatSampleDto> beats, out DateTime first, out DateTime last)
    {
        first = default;
        last = default;
        bool hasAny = false;

        foreach (var hb in beats)
        {
            if (string.IsNullOrEmpty(hb.measureAt)) continue;
            if (!DateTime.TryParse(hb.measureAt, out var t)) continue;

            if (!hasAny)
            {
                first = last = t;
                hasAny = true;
            }
            else
            {
                if (t < first) first = t;
                if (t > last) last = t;
            }
        }

        return hasAny;
    }


    async Task<List<QuestionHrAvgDto>> LoadQuestionHrAvgAsync(string reportId)
    {
        var url = $"{HttpClientBase.BaseUrl}/api/report/{reportId}/heartbeat/questions-avg";
        var (status, text, result, error) =
            await HttpClientBase.GetAuto(url, auth: true);

        if (result != UnityWebRequest.Result.Success || status >= 400)
        {
            Debug.LogWarning($"GET {url} failed {status} {error}\n{text}");
            return null;
        }

        var env = JsonUtility.FromJson<QuestionHrAvgEnvelope>(text);
        return env?.data;
    }


    public static float[] ToRadarArray(ScoresDto s)
    {
        if (s == null) return null;

        // 5점 척도 → 100점 척도 (클램프로 방어)
        float S(float v) => Mathf.Clamp(v, 0f, 5f) * 20f;

        // 차트 라벨/축 순서에 맞춰 매핑
        return new float[] {
        S(s.Communication),
        S(s.Adaptability),
        S(s.Teamwork_Leadership),
        S(s.Job_Competency),
        S(s.Integrity)
    };
    }

    IEnumerator SavePdfCoroutine()
    {
        var safeCompany = string.IsNullOrWhiteSpace(tCompany.text) ? "Unknown" : tCompany.text;
        var safeJob = string.IsNullOrWhiteSpace(tJob.text) ? "Unknown" : tJob.text;
        var fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmm}_{safeCompany}_{safeJob}.pdf";
        var path = Path.Combine(Application.persistentDataPath, fileName);

        if (footer) footer.SetActive(false);
        Canvas.ForceUpdateCanvases();
        yield return null;

        var pages = new List<RectTransform>();

        // 전체 탭
        ShowTab(-1);
        yield return null;
        pages.Add(captureRoot);

        // 문항 탭들
        int qCount = questionsRoot.childCount;
        for (int i = 0; i < qCount; i++)
        {
            ShowTab(i);
            yield return null;
            pages.Add(questionsRoot.GetChild(i).GetComponent<RectTransform>());
        }

        bool done = false; Exception err = null;
        yield return PdfExporterMulti.SaveUiPagesToPdfCoroutine(
            pages, path, 150,
            _ => done = true,
            ex => err = ex
        );

        if (footer) footer.SetActive(true);
        Canvas.ForceUpdateCanvases();

        if (err != null) Debug.LogException(err);
        else if (done) Debug.Log($"PDF 저장 완료: {path}");
    }

    void BuildQuestionPages(List<QnaItemDto> items, List<QuestionHrAvgDto> hrData)
    {
        // 1. 기존 생성된 질문 페이지(스크롤뷰)들 삭제
        for (int i = questionsRoot.childCount - 1; i >= 0; i--)
            Destroy(questionsRoot.GetChild(i).gameObject);

        // 데이터가 없으면 모든 질문 버튼 끄고 개요(Overview)만 보여줌
        if (items == null || items.Count == 0)
        {
            WireTabButtons(0); // 0개를 전달하여 모든 질문 버튼 비활성화
            ShowTab(-1);       // 개요 탭 열기
            return;
        }

        int builtPageCount = 0; // 실제 생성된 '질문 페이지(탭)' 수

        for (int i = 0; i < items.Count; i++)
        {
            var main = items[i];
            if (string.IsNullOrEmpty(main.question)) continue;

            // --- 페이지 프리팹 생성 ---
            var page = Instantiate(questionPagePrefab, questionsRoot);
            page.name = $"QuestionPage_{builtPageCount + 1}";

            int avgBpm = 0;
            if (hrData != null && builtPageCount < hrData.Count)
            {
                avgBpm = hrData[builtPageCount].avgBpm;
            }

            // 평균 BPM 등 헤더 텍스트 초기화
            var avgTxt = page.transform.Find("Header/AvgTxt")?.GetComponent<TMPro.TMP_Text>()
                     ?? page.transform.Find("Header/Avg")?.GetComponent<TMPro.TMP_Text>();

            if (avgTxt)
            {
                if (avgBpm > 0)
                    avgTxt.SetText($"평균 심박수 : {avgBpm} bpm");
                else
                    avgTxt.SetText("평균 심박수 : - bpm"); // 데이터 없으면 - 표시
            }

            // Content 영역 찾기
            var content = FindContent(page.transform);
            if (content == null)
            {
                Debug.LogWarning($"Content not found in {page.name}");
                continue;
            }

            // 기존 템플릿(예시 아이템) 제거
            for (int c = content.childCount - 1; c >= 0; c--)
                Destroy(content.GetChild(c).gameObject);

            // 1) 메인 질문/답변 추가
            SetQnA(
                Instantiate(qnaItemPrefab, content).transform,
                main.question,
                main.answer,
                main.labels
            );

            // 꼬리질문(Related Question)이 이어지면 같은 페이지에 추가
            int k = i + 1;
            while (k < items.Count && !string.IsNullOrEmpty(items[k].relatedQuestion))
            {
                var fu = items[k];
                SetQnA(
                    Instantiate(qnaItemPrefab, content).transform,
                    fu.relatedQuestion,
                    fu.answer,
                    fu.labels
                );
                k++;
            }

            // 꼬리질문 처리한 만큼 인덱스 점프
            i = k - 1;

            // 페이지 생성 카운트 증가
            builtPageCount++;
        }

        // 생성된 페이지 수만큼만 탭 버튼 활성화
        WireTabButtons(builtPageCount);

        // 첫 번째 질문 탭이 생성되었다면(built > 0) 1번 질문 탭을 열고, 아니면 개요 탭(-1)을 엶
        ShowTab(builtPageCount > 0 ? 0 : -1);
    }

    // 2) Content 찾기: 다양한 프리팹 구조 대응
    Transform FindContent(Transform root)
    {
        var t =
            root.Find("Table/Viewport/Content") ??
            root.Find("Scroll View/Viewport/Content") ??
            root.Find("ScrollView/Viewport/Content");
        if (t) return t;

        // 이름이 'Content'인 트랜스폼을 깊이 탐색
        var q = new Queue<Transform>();
        q.Enqueue(root);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (string.Equals(cur.name, "Content", StringComparison.OrdinalIgnoreCase))
                return cur;
            for (int i = 0; i < cur.childCount; i++)
                q.Enqueue(cur.GetChild(i));
        }
        return null;
    }

    // 3) 탭 버튼 연결: 개수만큼만 버튼을 살리고 클릭 시 ShowTab 호출
    void WireTabButtons(int activeCount)
    {
        // '전체(btnAll)' 버튼은 항상 활성화 및 리스너 연결
        if (btnAll)
        {
            btnAll.onClick.RemoveAllListeners();
            btnAll.onClick.AddListener(() => ShowTab(-1));
        }

        if (btnQuestions != null)
        {
            // btnQuestions 배열을 순회하며 activeCount보다 작은 인덱스만 SetActive(true)
            for (int i = 0; i < btnQuestions.Length; i++)
            {
                var btn = btnQuestions[i];
                if (!btn) continue;

                // 인덱스가 생성된 페이지 수보다 작으면 활성화 (예: 3개면 0,1,2 활성 / 3,4 비활성)
                bool shouldActive = (i < activeCount);

                btn.gameObject.SetActive(shouldActive);

                if (shouldActive)
                {
                    int pageIndex = i; // 클로저 캡처
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => ShowTab(pageIndex));

                    var txt = btn.GetComponentInChildren<TMP_Text>();
                    if(txt) txt.text = $"Q{pageIndex + 1}";
                }
            }
        }
    }


    // QnA 프리팹의 텍스트 바인딩(Question/Text, Answer/Text (TMP) 등 이름이 달라도 자식에서 TMP_Text를 찾아서 세팅)
    void SetQnA(Transform qnaRoot, string questionText, string answerText, IList<int> labels)
    {
        // 질문
        var qNode = qnaRoot.Find("Question");
        if (qNode)
        {
            var qText = qNode.GetComponentInChildren<TMP_Text>(true);
            if (qText)
            {
                qText.richText = true;
                qText.SetText(questionText ?? string.Empty);
            }
        }

        // 답변 (+ 감정 색상)
        var aNode = qnaRoot.Find("Answer");
        if (aNode)
        {
            var aText = aNode.GetComponentInChildren<TMP_Text>(true);
            if (aText)
            {
                aText.richText = true;

                // labels에 따라 문장별 색 적용
                string colored = BuildColoredAnswer(answerText ?? string.Empty, labels);
                aText.text = colored;
            }
        }
    }

    void ShowTab(int qIndex)
    {
        overviewPanel.SetActive(qIndex < 0);
        for (int i = 0; i < questionsRoot.childCount; i++)
            questionsRoot.GetChild(i).gameObject.SetActive(i == qIndex);
    }

    // 축 순서(라벨과 동일)
    static readonly string[] AxisKeys = {
    "Communication", "Adaptability", "Teamwork_Leadership", "Job_Competency", "Integrity"};
    static readonly string[] AxisLabelsKr = {
    "의사소통", "적응성", "팀워크", "직무 능력", "진실성"};

    // 딕셔너리 → 배열(순서 정렬)
    static float[] OrderScores(IDictionary<string, float> dict)
    {
        if (dict == null) return null;

        float S(float v) => Mathf.Clamp(v, 0f, 5f) * 20f;

        var arr = new float[AxisKeys.Length];
        for (int i = 0; i < AxisKeys.Length; i++)
        {
            if (!dict.TryGetValue(AxisKeys[i], out var v)) v = 0f;
            arr[i] = S(v);
        }
        return arr;
    }

    public static string MdToTmp(string md)
    {
        if (string.IsNullOrEmpty(md)) return "";

        var s = md;

        // 헤더(#, ##, ###) → 크기+볼드
        s = Regex.Replace(s, @"(?m)^\#\#\#\s+(.+)$", "<size=115%><b>$1</b></size>");
        s = Regex.Replace(s, @"(?m)^\#\#\s+(.+)$", "<size=125%><b>$1</b></size>");
        s = Regex.Replace(s, @"(?m)^\#\s+(.+)$", "<size=140%><b>$1</b></size>");

        // 볼드/이탤릭
        s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<b>$1</b>");   // **bold**
        s = Regex.Replace(s, @"\*(.+?)\*", "<i>$1</i>");   // *italic*

        // 목록(- )은 점 기호로 치환
        s = Regex.Replace(s, @"(?m)^\s*-\s+", "• ");

        // 코드블록/링크 등은 필요 시 추가 규칙 확장
        return s;
    }

    IEnumerator SavePdfWithPdfCanvasCoroutine()
    {
        if (_data == null) yield break;

        var safeJob = MakeSafeFileNamePart(tJob.text, "Job");
        var fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmm}_{safeJob}.pdf";
        var path = System.IO.Path.Combine(Application.persistentDataPath, fileName);

        if (footer) footer.SetActive(false);

        if (!pdfCanvas || !pdfPagesRoot || !pdfOverviewPagePrefab || !pdfQuestionPagePrefab)
        {
            Debug.LogWarning("PDF export refs not set");
            yield break;
        }

        var hiddenCamObj = new GameObject("PdfLayoutCam_Hidden");
        var hiddenCam = hiddenCamObj.AddComponent<Camera>();

        // 화면 해상도와 동일한 임시 텍스처 생성
        var dummyRt = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);

        hiddenCam.targetTexture = dummyRt; // 렌더링 결과를 텍스처로 보냄 (화면 표시 X)
        hiddenCam.enabled = true;          // 카메라는 켜둠 (UI 갱신 O)
        hiddenCam.backgroundColor = Color.white;
        hiddenCam.clearFlags = CameraClearFlags.SolidColor;
        hiddenCam.orthographic = true;

        // 기존 상태 백업
        var oldRenderMode = pdfCanvas.renderMode;
        var oldWorldCamera = pdfCanvas.worldCamera;

        // 캔버스 연결
        pdfCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        pdfCanvas.worldCamera = hiddenCam;

        pdfCanvas.gameObject.SetActive(true);
        pdfPagesRoot.gameObject.SetActive(true);

        for (int i = pdfPagesRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(pdfPagesRoot.GetChild(i).gameObject);
        }

        var pages = new List<RectTransform>();

        var overview = Instantiate(pdfOverviewPagePrefab, pdfPagesRoot);
        overview.gameObject.SetActive(false);
        overview.Bind(_data, tCompany.text, tJob.text, tDate.text, _questionRanges);

        var rtOverview = overview.GetComponent<RectTransform>();
        if (rtOverview != null) pages.Add(rtOverview);

        BuildPdfQuestionPages(pages);

        foreach (var rt in pages)
        {
            if (rt && !rt.gameObject.activeSelf)
                rt.gameObject.SetActive(true);
        }

        // 레이아웃 안정화 대기 (카메라가 켜져있으므로 정상 계산됨)
        for (int i = 0; i < 3; i++)
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();
        }

        // 확실한 업데이트를 위해 강제 렌더링 호출
        hiddenCam.Render();

        yield return new WaitForSecondsRealtime(0.1f);

        bool done = false;
        Exception err = null;

        // 캡처 수행
        yield return PdfExporterMulti.SaveUiPagesToPdfCoroutine(
            pages,
            path,
            150,
            _ => done = true,
            ex => err = ex
        );

        // [복구 및 정리]
        pdfCanvas.gameObject.SetActive(false);
        pdfCanvas.renderMode = oldRenderMode;
        pdfCanvas.worldCamera = oldWorldCamera;

        hiddenCam.targetTexture = null;
        RenderTexture.ReleaseTemporary(dummyRt); // 임시 텍스처 해제
        Destroy(hiddenCamObj);

        if (footer) footer.SetActive(true);
        Canvas.ForceUpdateCanvases();

        if (err != null)
        {
            Debug.LogException(err);
        }
        else if (done)
        {
            Debug.Log($"PDF 저장 완료: {path}");
        }
    }

    void BuildPdfQuestionPages(List<RectTransform> outPages)
    {
        if (_data == null || !pdfQuestionPagePrefab || !pdfPagesRoot) return;

        var items = _data?.qnaList;
        if (items == null || items.Count == 0) return;

        int pageIndex = 0;

        for (int i = 0; i < items.Count; i++)
        {
            var main = items[i];
            if (string.IsNullOrEmpty(main.question)) continue;  // 메인 질문만 페이지 시작

            // 메인 + 꼬리질문 묶음 만들기
            var followUps = new List<QnaItemDto>();
            int k = i + 1;
            while (k < items.Count && !string.IsNullOrEmpty(items[k].relatedQuestion))
            {
                followUps.Add(items[k]);
                k++;
            }
            i = k - 1;   // 소비한 만큼 인덱스 점프

            int avgBpm = 0;
            if (_questionHrData != null && pageIndex < _questionHrData.Count)
            {
                avgBpm = _questionHrData[pageIndex].avgBpm;
            }

            // 페이지 프리팹 생성
            var page = Instantiate(pdfQuestionPagePrefab, pdfPagesRoot);
            page.name = $"PdfQuestion{pageIndex + 1}";
            page.gameObject.SetActive(true);

            // QnA 프리팹 선택 (pdf용이 있으면 우선)
            if (pdfQnaItemPrefab)
                page.qnaItemPrefab = pdfQnaItemPrefab;
            else if (qnaItemPrefab)
                page.qnaItemPrefab = qnaItemPrefab;

            page.Bind(pageIndex + 1, main, followUps, avgBpm);

            outPages.Add(page.GetComponent<RectTransform>());
            pageIndex++;
        }
    }

    string MakeSafeFileNamePart(string raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        // "회사 | 삼성" 처럼 앞에 라벨 붙은 것 잘라내기
        int barIndex = raw.LastIndexOf('|');
        if (barIndex >= 0 && barIndex + 1 < raw.Length)
            raw = raw.Substring(barIndex + 1).Trim();  // "삼성"

        // 파일 이름에 쓸 수 없는 문자 제거/치환
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        }

        var result = sb.ToString().Trim();
        if (string.IsNullOrEmpty(result))
            result = fallback;

        // 너무 길면 잘라주기 (선택)
        if (result.Length > 20)
            result = result.Substring(0, 20);

        return result;
    }

    /// <summary>
    /// answer 문자열을 . 와 , 기준으로 분리해 각 조각에 labels[i]에 따른 색을 적용해서
    /// TMP RichText 문자열로 만들어준다.
    /// 0=중립(흰색), 1=부정(빨간색), 2=긍정(파란색)
    /// labels가 없거나 더 적으면 남는 문장은 중립색으로 처리.
    /// </summary>
    public static string BuildColoredAnswer(string answerText, IList<int> labels)
    {
        if (string.IsNullOrEmpty(answerText))
            return string.Empty;

        // . , 기준으로 문장 분리
        var segments = SplitAnswerSegments(answerText);
        if (segments.Count == 0)
            segments.Add(answerText);

        var sb = new StringBuilder(answerText.Length + 32);
        int labelCount = (labels != null) ? labels.Count : 0;
        int labelIndex = 0;
        bool first = true;

        foreach (var segRaw in segments)
        {
            if (string.IsNullOrWhiteSpace(segRaw))
                continue;

            if (!first)
                sb.Append(' ');
            first = false;

            // label 결정 (없으면 0=중립)
            int label = 0;
            if (labelIndex < labelCount)
                label = labels[labelIndex];
            labelIndex++;

            string colorHex;
            switch (label)
            {
                case 1: // 부정
                    colorHex = "#FFFFFF"; // 빨간색 FF5555
                    break;
                case 2: // 긍정
                    colorHex = "#FFFFFF"; // 파란색 5599FF
                    break;
                default: // 중립
                    colorHex = "#FFFFFF"; // 흰색
                    break;
            }

            // TMP 리치텍스트 안전하게: 꺾쇠만 이스케이프
            string seg = segRaw.Replace("<", "&lt;").Replace(">", "&gt;");

            sb.Append("<color=").Append(colorHex).Append(">");
            sb.Append(seg);
            sb.Append("</color>");
        }

        return sb.ToString();
    }


    /// <summary>
    /// answer 전체 문자열을 . , ? ! 기준으로 문장 단위로 분리한다.
    /// "..." 처럼 연속된 . 은 하나의 문장 끝으로만 취급.
    /// </summary>
    static List<string> SplitAnswerSegments(string text)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
            return result;

        var sb = new StringBuilder();

        // 문장 구분 문자 세트
        // . , ? !  (+ 필요하면 '…' 도 추가 가능)
        bool IsDelim(char ch) => ch == '.' || ch == ',' || ch == '?' || ch == '!';

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            sb.Append(ch);

            bool isDelim = IsDelim(ch);
            bool nextIsDelim = (i + 1 < text.Length) && IsDelim(text[i + 1]);

            // 구분자(.,?! 등)가 나오고, 바로 뒤가 또 구분자가 아니면
            // 하나의 문장이 끝난 것으로 본다.
            if (isDelim && !nextIsDelim)
            {
                var seg = sb.ToString().TrimStart();
                if (!string.IsNullOrWhiteSpace(seg))
                    result.Add(seg);
                sb.Length = 0;
            }
        }

        // 마지막에 남은 버퍼도 문장으로 추가
        if (sb.Length > 0)
        {
            var seg = sb.ToString().TrimStart();
            if (!string.IsNullOrWhiteSpace(seg))
                result.Add(seg);
        }

        return result;
    }



}
