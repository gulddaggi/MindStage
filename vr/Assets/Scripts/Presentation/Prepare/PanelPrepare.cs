using App.Core;                 // ResumeListItem
using App.Infra;
using App.Presentation.Settings;// PanelAudioAndMic / PanelWatch에서 쓴 네임스페이스 사용중이라면 맞춰줘
using App.Services;
using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.Presentation.Interview
{
    public enum InterviewMode
    {
        QuestionSet,      // 기존 질문 세트 기반 AI 면접
        SelfIntro1Min     // 1분 자기소개
    }

    /// <summary>VR 면접 준비 메인 패널.</summary>
    public class PanelPrepare : MonoBehaviour
    {
        [Header("Buttons")]
        public Button btnQuestion;
        public Button btnAudioMic;
        public Button btnWatch;
        public Button btnDone;
        public Button btnBack;

        [Header("Labels")]
        public TMP_Text questionSummary; // 버튼 아래 설명 텍스트
        public TMP_Text doneHint;        // "설정을 진행해주세요" / "설정이 완료되었습니다"

        [Header("Question Button Label")]
        public TMP_Text questionButtonLabel;

        [Header("Popups")]
        public PopupQuestionSelect popupQuestion; // 새로 추가(아래 2번 코드)
        public PanelAudioAndMic popupAudio;       // 기존 PanelAudioAndMic 프리팹
        public PanelWatch popupWatch;             // 기존 PanelWatch 프리팹
        public PanelQuestionSelect popupTail;
        public PopupInterviewMode popupMode;

        // 내부 상태
        bool _questionSelected = false;
        bool _watchReady = false;
        string _selectedResumeId;

        IResumeService _resume;
        IWearLinkService _wear;

        bool _bootstrapped;        // 초기화 1회 가드
        string _selectedSummary;   // UI 요약 텍스트

        // 선택된 데이터 임시 저장
        int _selectedJobPostingId = 0; // 1분 자기소개 모드용

        void Awake()
        {
            _wear = App.Infra.Services.Resolve<IWearLinkService>();
            _resume = App.Infra.Services.Resolve<IResumeService>();

            if (popupQuestion) popupQuestion.gameObject.SetActive(false);
            if (popupAudio) popupAudio.gameObject.SetActive(false);
            if (popupWatch) popupWatch.gameObject.SetActive(false);
            if (popupTail) popupTail.gameObject.SetActive(false);
            if (popupMode) popupMode.gameObject.SetActive(false);

            // 질문 목록 로더 주입 (모드별로 분리 가능하게)
            if (popupQuestion != null && _resume is ResumeApiService resumeApi)
            {
                popupQuestion.ConfigureLoaders(
                    // 1. 기본 모드: 내 자소서/면접 목록 (/api/resume/me)
                    interviewLoader: () => _resume.GetListAsync(),

                    // 2. 1분 자기소개 모드: 채용 공고 목록 (/api/JobPosting/list)
                    // ResumeApiService에 추가한 GetJobPostingsAsync 호출
                    selfIntroLoader: () => resumeApi.GetJobPostingsAsync()
                );
            }

            btnQuestion.onClick.AddListener(OnClickQuestion);
            btnAudioMic.onClick.AddListener(() => popupAudio.gameObject.SetActive(true));
            btnWatch.onClick.AddListener(OnClickWatch);
            btnDone.onClick.AddListener(OnClickDone);
            btnBack.onClick.AddListener(async () =>
            {
                await SceneLoader.LoadSingleAsync(SceneIds.MainMenu);
            });
        }

        void OnEnable()
        {
            // 모드 팝업 콜백 연결
            if (popupMode != null)
                popupMode.OnSelected = OnModeSelected;

            // 질문 팝업 선택 결과 구독
            if (popupQuestion)
                popupQuestion.OnSelected = OnQuestionSelected;

            // 처음만 기본 문구로 초기화. 이후에는 유지
            if (!_bootstrapped)
            {
                // 기본 모드는 질문 세트 기반 AI 면접
                InterviewStartContext.Clear();
                InterviewStartContext.SetMode(InterviewMode.QuestionSet);

                _questionSelected = false;
                _selectedResumeId = null;
                _selectedSummary = null;
                _bootstrapped = true;

                ApplyModeToUi();  // 모드에 따라 라벨/안내문 설정

                // 첫 진입 시 모드 선택 팝업 오픈
                if (popupMode != null)
                    popupMode.Open(InterviewStartContext.Mode);
            }
            else
            {
                // 씬 재활성화 시 기존 모드/선택 유지
                ApplyModeToUi();

                // 이전 선택 상태를 UI에 재적용
                if (!string.IsNullOrEmpty(_selectedSummary))
                    questionSummary.text = _selectedSummary;
            }

            // 워치 상태는 최신으로 갱신하되, 질문 선택은 건드리지 않음
            _ = RefreshWatchState();
            UpdateReadyUi();
        }

        void OnModeSelected(InterviewMode mode)
        {
            // 모드 변경 시 기존 질문 선택은 리셋
            InterviewStartContext.SetMode(mode);

            _questionSelected = false;
            _selectedSummary = null;
            _selectedResumeId = null;

            ApplyModeToUi();
            UpdateReadyUi();
        }

        void ApplyModeToUi()
        {
            // 버튼 라벨 변경
            if (questionButtonLabel != null)
            {
                questionButtonLabel.text = InterviewStartContext.Mode == InterviewMode.SelfIntro1Min
                    ? "자기소개 회사 선택"
                    : "질문 세트 선택";
            }

            // 아직 아무 것도 선택 안 된 상태라면 안내 문구도 모드에 맞게
            if (!_questionSelected && questionSummary != null)
            {
                questionSummary.text = InterviewStartContext.Mode == InterviewMode.SelfIntro1Min
                    ? "<color=#FF6B6B>회사/직무를 선택해주세요</color>"
                    : "<color=#FF6B6B>질문을 선택해주세요</color>";
            }

            // 질문 선택 팝업에도 모드 전달
            if (popupQuestion != null)
                popupQuestion.SetMode(InterviewStartContext.Mode);
        }

        void OnClickQuestion()
        {
            if (!popupQuestion) return;

            popupQuestion.SetMode(InterviewStartContext.Mode);
            popupQuestion.gameObject.SetActive(true);
            popupQuestion.Open();
        }

        async void OnClickWatch()
        {
            if (!popupWatch) return;
            popupWatch.gameObject.SetActive(true);
            // PanelWatch 안에서 등록/해제 후 닫히면, 여기서 상태 갱신
            popupWatch.onClosed = async () =>
            {
                await RefreshWatchState();
                UpdateReadyUi();
            };
        }

        async Task RefreshWatchState()
        {
            // /api/GalaxyWatch/me 조회로 현재 등록 여부 판단
            var me = await _wear.GetStatusAsync();
            _watchReady = me != null; // data=null이면 미등록
        }

        // 질문 팝업에서 콜백 받는 곳
        void OnQuestionSelected(ResumeListItem item, string prettySummary)
        {
            _selectedSummary = prettySummary;
            _questionSelected = true;
            questionSummary.text = prettySummary;

            if (InterviewStartContext.Mode == InterviewMode.SelfIntro1Min)
            {
                // 1분 자기소개 모드: JobPostingId 저장 (item.id에 매핑됨)
                int.TryParse(item.id, out _selectedJobPostingId);

                // 아직 면접 생성 전이므로 InterviewId는 없음
                InterviewStartContext.Set(0);
            }
            else
            {
                // 기존 모드: InterviewId 저장
                int parsed = 0;
                if (!string.IsNullOrEmpty(item.interviewId))
                    int.TryParse(item.interviewId, out parsed);

                _selectedJobPostingId = 0;
                InterviewStartContext.Set(parsed);
            }

            UpdateReadyUi();
        }

        void UpdateReadyUi()
        {
            var ready = _questionSelected && _watchReady;

            btnDone.interactable = ready;
            doneHint.text = ready
                ? "<color=#2ECC71>설정이 완료되었습니다</color>"
                : "<color=#FF6B6B>설정을 진행해주세요.</color>";
        }

        private async void OnClickDone()
        {
            if (btnDone == null || !btnDone.interactable) return;

            btnDone.interactable = false;

            // 1. 1분 자기소개 모드: API 호출 후 이동
            if (InterviewStartContext.Mode == InterviewMode.SelfIntro1Min)
            {
                if (doneHint) doneHint.text = "면접 생성 중...";

                var apiService = _resume as ResumeApiService;
                if (apiService != null)
                {
                    // POST /api/Interview/demo/create 호출
                    var result = await apiService.CreateDemoInterviewAsync(_selectedJobPostingId);

                    if (result != null)
                    {
                        // 응답 데이터 Context에 저장
                        InterviewStartContext.Set(result.interviewId);
                        InterviewStartContext.SetDemoData(
                            result.questionId,
                            result.questionPresignedUrl,
                            result.exampleIntroduction
                        );

                        InterviewStartContext.SetFollowups(true);
                        await SceneLoader.LoadSingleAsync(SceneIds.Interview);
                        return;
                    }
                    else
                    {
                        if (doneHint) doneHint.text = "<color=red>면접 생성 실패</color>";
                        btnDone.interactable = true;
                        return;
                    }
                }
            }

            // 2. 기존 모드: 꼬리질문 팝업 후 이동
            if (doneHint) doneHint.text = "로딩 중...";

            if (popupTail != null)
            {
                popupTail.Open(async useTail =>
                {
                    InterviewStartContext.SetFollowups(useTail);
                    await SceneLoader.LoadSingleAsync(SceneIds.Interview);
                });
            }
            else
            {
                InterviewStartContext.SetFollowups(false);
                await SceneLoader.LoadSingleAsync(SceneIds.Interview);
                btnDone.interactable = true;
            }
        }
    }

    public static class InterviewStartContext
    {
        public static int InterviewId { get; private set; }
        public static bool Followups { get; private set; }
        public static InterviewMode Mode { get; private set; } = InterviewMode.QuestionSet;

        public static int DemoQuestionId { get; private set; }
        public static string DemoAudioUrl { get; private set; }
        public static string DemoExampleText { get; private set; }

        public static void Set(int id) => InterviewId = id;
        public static void SetFollowups(bool on) => Followups = on;
        public static void SetMode(InterviewMode m) => Mode = m;

        public static void SetDemoData(int qId, string url, string example)
        {
            DemoQuestionId = qId;
            DemoAudioUrl = url;
            DemoExampleText = example;
        }

        public static void Clear()
        {
            InterviewId = 0;
            Followups = false;
            Mode = InterviewMode.QuestionSet;

            DemoQuestionId = 0;
            DemoAudioUrl = null;
            DemoExampleText = null;
        }
    }
}
