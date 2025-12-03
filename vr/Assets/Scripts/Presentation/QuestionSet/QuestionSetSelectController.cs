using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Services;
using App.Infra;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>회사→직무 선택으로 질문 세트를 확정하고 Interview 씬으로 전환하는 컨트롤러.</summary>

namespace App.Presentation.QuestionSet
{
    public class QuestionSetSelectController : MonoBehaviour
    {
        [Header("Filter")]
        public TMP_Dropdown CompanyDropdown;
        public TMP_Dropdown JobDropdown;

        [Header("Actions")]
        public Button BtnConfirm;   // 선택 확정 → Interview
        public Button BtnResume;    // 자기소개서 등록으로 이동

        [Header("Preview (optional)")]
        public TMP_Text TxtPreview; // "문항 N개" 같은 미리보기 표기(없으면 null)

        ILookupService _lookup;
        IQuestionSetService _qsets;

        App.Core.Company[] _companies;
        App.Core.JobRole[] _jobs;

        async void Start()
        {
            _qsets = App.Infra.Services.Resolve<IQuestionSetService>();

            _lookup = App.Infra.Services.Resolve<ILookupService>();

            // 회사 로딩
            _companies = await _lookup.GetCompaniesAsync();
            CompanyDropdown.ClearOptions();
            CompanyDropdown.AddOptions(_companies.Select(c => c.name).ToList());

            CompanyDropdown.onValueChanged.AddListener(async _ => {
                var compId = _companies.ElementAtOrDefault(CompanyDropdown.value)?.id;
                await LoadJobs(compId);
            });

            // 최초 1회 트리거
            var firstCompanyId = _companies.ElementAtOrDefault(0)?.id;
            await LoadJobs(firstCompanyId);

            // 버튼
            BtnConfirm.onClick.AddListener(OnConfirm);
            BtnResume.onClick.AddListener(async () => {
                await SceneLoader.LoadSingleAsync(SceneIds.ResumeEdit);
            });

            // 초기 미리보기
            await RefreshPreview();
            JobDropdown.onValueChanged.AddListener(async _ => await RefreshPreview());
        }

        async Task LoadJobs(string companyId)
        {
            JobDropdown.ClearOptions();
            if (string.IsNullOrEmpty(companyId)) return;

            _jobs = await _lookup.GetJobsAsync(companyId);
            JobDropdown.AddOptions(_jobs.Select(j => j.name).ToList());
            JobDropdown.value = 0;
            await RefreshPreview();
        }

        async Task RefreshPreview()
        {
            if (TxtPreview == null || _jobs == null || _jobs.Length == 0) return;
            var jobId = _jobs.ElementAtOrDefault(JobDropdown.value)?.id;
            if (string.IsNullOrEmpty(jobId)) { TxtPreview.text = "미리보기 없음"; return; }

            // 더미의 질문 API 그대로 사용
            var qs = await _lookup.GetQuestionsByJobAsync(jobId);
            TxtPreview.text = $"문항 {qs.Length}개";
        }

        async void OnConfirm()
        {
            var companyId = _companies?.ElementAtOrDefault(CompanyDropdown.value)?.id;
            var jobId = _jobs?.ElementAtOrDefault(JobDropdown.value)?.id;

            if (string.IsNullOrEmpty(companyId) || string.IsNullOrEmpty(jobId))
            {
                Debug.LogWarning("회사/직무 선택이 올바르지 않습니다.");
                return;
            }

            // ★ 새 세트 생성(직무의 현재 질문 스냅샷을 고정) + 순번 포함 ID
            var detail = await _qsets.CreateNewAsync(companyId, jobId);

            // 다음 씬에서 사용할 세트ID 저장
            PlayerPrefs.SetString("interview.set.id", detail.id);
            PlayerPrefs.SetString("interview.set.companyId", companyId);
            PlayerPrefs.SetString("interview.set.jobId", jobId);
            PlayerPrefs.Save();

            _ = App.Infra.SceneLoader.LoadSingleAsync(App.Infra.SceneIds.Interview);
        }
    }
}
