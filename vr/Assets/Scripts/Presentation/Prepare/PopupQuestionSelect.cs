using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using App.Core;

namespace App.Presentation.Interview
{
    /// <summary>질문 세트 선택 팝업.</summary>
    public class PopupQuestionSelect : MonoBehaviour
    {
        [Header("UI")]
        public Button btnClose;
        public Transform listRoot;     // Vertical Layout Group
        public GameObject rowPrefab;   // TableRow_Resume 프리팹

        public Action<ResumeListItem, string> OnSelected;

        InterviewMode _mode = InterviewMode.QuestionSet;

        Func<Task<ResumeListItem[]>> _loadInterview;
        Func<Task<ResumeListItem[]>> _loadSelfIntro;

        //IResumeService ResumeSvc;

        void Awake()
        {
            //ResumeSvc = App.Infra.Services.Resolve<IResumeService>();
            if (btnClose) btnClose.onClick.AddListener(() => gameObject.SetActive(false));
        }

        public void ConfigureLoaders(
    Func<Task<ResumeListItem[]>> interviewLoader,
    Func<Task<ResumeListItem[]>> selfIntroLoader)
        {
            _loadInterview = interviewLoader;
            _loadSelfIntro = selfIntroLoader;
        }

        public void SetMode(InterviewMode mode)
        {
            _mode = mode;
        }

        public async void Open()
        {
            gameObject.SetActive(true);
            _ = BuildListAsync();
        }

        async Task BuildListAsync()
        {
            // 기존 행 제거
            if (listRoot != null)
            {
                foreach (Transform child in listRoot)
                    Destroy(child.gameObject);
            }

            ResumeListItem[] list = Array.Empty<ResumeListItem>();

            Debug.Log($"[Popup] BuildListAsync 시작. 현재 모드: {_mode}");

            try
            {
                Func<Task<ResumeListItem[]>> loader =
                    _mode == InterviewMode.SelfIntro1Min
                        ? _loadSelfIntro
                        : _loadInterview;

                if (loader == null)
                {
                    Debug.LogWarning(
                        $"PopupQuestionSelect: loader for mode {_mode} 가 설정되지 않았습니다. " +
                        $"PanelPrepare에서 ConfigureLoaders 호출 여부를 확인하세요.");
                    return;
                }

                list = await loader();
                Debug.Log($"[Popup] 로더 실행 완료. 가져온 아이템 수: {list?.Length ?? 0}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"PopupQuestionSelect.BuildListAsync() 실패: {ex}");
            }

            //var list = await ResumeSvc.GetListAsync(); // /api/resume/me
            if (list == null || list.Length == 0)
            {
                Debug.LogWarning("[Popup] 표시할 리스트가 없습니다.");
                return;
            }

            foreach (var it in list.OrderByDescending(x => x.modifiedAt))
            {
                var go = Instantiate(rowPrefab, listRoot);

                // 자식 텍스트 바인딩 (TableRow_Resume 구조에 맞춰서)
                var tCompany = go.transform.Find("Company")?.GetComponent<TMP_Text>();
                var tJob = go.transform.Find("Job")?.GetComponent<TMP_Text>();
                var tModified = go.transform.Find("Modified")?.GetComponent<TMP_Text>();
                var btnSelect = go.transform.Find("BtnOpen")?.GetComponent<Button>();
                var btnText = go.transform.Find("BtnOpen/Text (TMP)")?.GetComponent<TMP_Text>();

                if (tCompany) tCompany.text = it.companyName;
                if (tJob) tJob.text = it.jobName;
                if (tModified)
                {
                    tModified.text = string.IsNullOrEmpty(it.modifiedAt)
                        ? "-"
                        : TryPrettyDate(it.modifiedAt);
                }
                // interviewId가 NULL이면 선택 불가
                bool selectable = string.Equals(it.progressStatus, "NOT_STARTED", StringComparison.OrdinalIgnoreCase);

                if (btnSelect) btnSelect.interactable = selectable;
                if (btnText) btnText.text = selectable ? "선택" : "선택 불가";

                if (btnSelect)
                {
                    btnSelect.onClick.AddListener(() =>
                    {
                        if (!selectable) return;
                        var summary =
$@"{it.companyName} | {it.jobName}
{TryPrettyDate(it.modifiedAt)}";
                        OnSelected?.Invoke(it, summary);
                        gameObject.SetActive(false);
                    });
                }

                //Debug.Log($"[ResumeRow] id={it.id}, interviewId={it.interviewId}, enable={(!string.IsNullOrEmpty(it.interviewId))}");
            }
        }

        static string TryPrettyDate(string iso)
        {
            if (DateTime.TryParse(iso, out var dt))
                return dt.ToString("yy-MM-dd\nHH:mm");
            return iso ?? "";
        }
    }
}
