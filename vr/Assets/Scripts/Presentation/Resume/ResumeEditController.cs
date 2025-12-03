using App.Core;
using App.Infra;
using App.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//// <summary>자기소개서 생성·수정 화면. 편집 모드에선 회사/직무를 고정한다.</summary>

public class ResumeEditController : MonoBehaviour
{
    [Header("Selectors")]
    public TMP_Dropdown ddCompany;
    public TMP_Text tCompany;
    public TMP_Dropdown ddJob;
    public TMP_Text tJob;
    public Button btnSelect;

    [Tooltip("드롭다운을 감싼 행(레이블+드롭다운)")]
    public GameObject companyRow;
    public GameObject jobRow;

    [Header("Questions")]
    public Transform answersContent;  // ScrollView Content
    public GameObject answerRowPrefab;

    [Header("Actions")]
    public Button btnSave;
    public Button btnCancel;

    ILookupService _lookup;
    IResumeService _resume;

    Company[] _companies;
    JobRole[] _jobs;
    Question[] _questions;

    ResumeDetail _model = new();
    string _editingId;                    // null이면 신규 작성
    bool _navigating;
    bool _suppress;                       // 드롭다운 값 세팅 중 이벤트 억제
    List<AnswerItem> _prefillAnswers;     // 편집 모드: 기존 답변 스냅샷

    bool _readOnly = false;

    void Awake()
    {
        // 인스펙터 미지정 시 드롭다운 부모를 행으로 사용
        if (companyRow == null && ddCompany) companyRow = ddCompany.gameObject;
        if (jobRow == null && ddJob) jobRow = ddJob.gameObject;
    }

    async void Start()
    {
        _lookup = Services.Resolve<ILookupService>();
        _resume = Services.Resolve<IResumeService>();

        btnSave.onClick.AddListener(() => _ = OnSave());
        btnCancel.onClick.AddListener(() => _ = OnClickCancel());

        ddCompany.onValueChanged.AddListener(async _ => { if (!_suppress) await OnCompanyChanged(); });
        ddJob.onValueChanged.AddListener(async _ => { if (!_suppress) await OnJobChanged(); });

        _editingId = PlayerPrefs.GetString("resume.edit.id", null);
        if (!string.IsNullOrEmpty(_editingId)) PlayerPrefs.DeleteKey("resume.edit.id");

        await LoadCompanies();


        if (!string.IsNullOrEmpty(_editingId))
        {
            // ===== 상세 보기 모드 =====
            _model = await _resume.GetAsync(_editingId);

            // 드롭다운/저장 비활성 & 행 숨김
            if (companyRow) companyRow.SetActive(false);
            if (jobRow) jobRow.SetActive(false);
            if (ddCompany) ddCompany.interactable = false;
            if (ddJob) ddJob.interactable = false;
            if (btnSave) btnSave.gameObject.SetActive(false);
            tCompany.gameObject.SetActive(false);
            tJob.gameObject.SetActive(false);
            btnSelect.gameObject.SetActive(false);

            // 서버 문항/답으로 바로 테이블 구성
            _prefillAnswers = _model.answers?.ToList() ?? new List<AnswerItem>();
            _questions = _model.questions ?? Array.Empty<Question>();
            BuildAnswerRows(_questions); // 기존 함수 그대로 활용

            // 모든 답변 입력란 읽기 전용
            foreach (Transform ch in answersContent)
            {
                var input = ch.Find("Input_Answer")?.GetComponent<TMP_InputField>();
                if (input) { input.readOnly = true; input.interactable = false; }
            }
        }
        else
        {
            // ===== 신규 작성 모드 =====
            if (companyRow) companyRow.SetActive(true);
            if (jobRow) jobRow.SetActive(true);
            tCompany.gameObject.SetActive(true);
            tJob.gameObject.SetActive(true);
            btnSelect.gameObject.SetActive(true);
        }
    }

    void ApplyReadOnlyUI(bool on)
    {
        _readOnly = on;

        if (ddCompany) { ddCompany.interactable = !on; ddCompany.enabled = !on; }
        if (ddJob) { ddJob.interactable = !on; ddJob.enabled = !on; }

        if (btnSave) btnSave.gameObject.SetActive(!on);
        // 취소/뒤로는 유지
    }

    async Task LoadCompanies()
    {
        _companies = await _lookup.GetCompaniesAsync();
        ddCompany.options = _companies.Select(c => new TMP_Dropdown.OptionData(c.name)).ToList();
        if (_companies.Length > 0) ddCompany.value = 0;
        await OnCompanyChanged();
    }

    async Task OnCompanyChanged()
    {
        var selCompanyId = _companies?.ElementAtOrDefault(ddCompany.value)?.id;
        _jobs = string.IsNullOrEmpty(selCompanyId)
            ? Array.Empty<JobRole>()
            : await _lookup.GetJobsAsync(selCompanyId);

        ddJob.options = _jobs.Select(j => new TMP_Dropdown.OptionData(j.name)).ToList();
        ddJob.value = 0;
        await OnJobChanged();
    }

    async Task OnJobChanged()
    {
        var selJobId = _jobs?.ElementAtOrDefault(ddJob.value)?.id; // ← jobPostingId 문자열
        _model.companyId = _companies?.ElementAtOrDefault(ddCompany.value)?.id;
        _model.jobId = selJobId;

        _questions = string.IsNullOrEmpty(selJobId)
            ? Array.Empty<Question>()
            : await _lookup.GetQuestionsByJobAsync(selJobId);

        BuildAnswerRows(_questions); // 네가 쓰는 프리팹에 맞춰 번호/문항/입력칸 세팅
    }

    void BuildAnswerRows(Question[] qs)
    {
        foreach (Transform ch in answersContent) Destroy(ch.gameObject);
        //_model.answers.Clear();

        if (qs == null) return;

        foreach (var q in qs)
        {
            var go = Instantiate(answerRowPrefab, answersContent);
            go.name = q.id; // 편집 시 매칭 용이

            var goHeader = go.transform.Find("Header");

            goHeader.transform.Find("Number").GetComponent<TMP_Text>().text = q.number;
            goHeader.transform.Find("Question").GetComponent<TMP_Text>().text = q.text;

            var input = go.transform.Find("Input_Answer").GetComponent<TMP_InputField>();

            // 편집 모드라면 기존 답변 주입 + 모델에도 유지
            var prev = _prefillAnswers?.FirstOrDefault(a => a.questionId == q.id);
            if (prev != null)
            {
                input.text = prev.answer ?? "";
                _model.answers.Add(new AnswerItem { questionId = q.id, answer = prev.answer ?? "" });
            }

            // 입력 변경 → 모델 반영(신규/수정 공통)
            input.onValueChanged.AddListener(val =>
            {
                var exist = _model.answers.FirstOrDefault(a => a.questionId == q.id);
                if (exist == null)
                    _model.answers.Add(new AnswerItem { questionId = q.id, answer = val });
                else
                    exist.answer = val;
            });
        }

        // 한 번 사용한 스냅샷은 비움
        _prefillAnswers = null;
    }

    void SelectDropdown(TMP_Dropdown dd, string targetId, string[] ids)
    {
        var idx = System.Array.IndexOf(ids, targetId);
        if (idx >= 0) dd.value = idx;
    }

    async Task OnSave()
    {
        // _questions 순서대로 입력칸에서 텍스트를 모아 AnswerItem 만들기
        var answers = new List<AnswerItem>();
        for (int i = 0; i < _questions.Length; i++)
        {
            var rowTf = answersContent.GetChild(i);
            var input = rowTf.Find("InputField")?.GetComponent<TMP_InputField>()
                       ?? rowTf.Find("Input_Answer")?.GetComponent<TMP_InputField>(); // 프리팹 이름 호환

            var text = input ? input.text : string.Empty;
            answers.Add(new AnswerItem
            {
                questionId = _questions[i].id,  // 서버 questionId 그대로 사용
                answer = text ?? string.Empty
            });
        }

        var payload = new ResumeDetail
        {
            companyId = _model.companyId,
            jobId = _model.jobId,     // === jobPostingId 문자열 ===
            questions = _questions,
            answers = answers
        };

        var created = await _resume.CreateAsync(payload);
        if (created != null)
        {
            // 저장 성공 → 목록으로
            await SceneLoader.LoadSingleAsync(SceneIds.ResumeList);
        }
    }

    async Task OnClickCancel()
    {
        if (_navigating) return;
        _navigating = true;
        btnCancel.interactable = false;
        try { await SceneLoader.LoadSingleAsync(SceneIds.ResumeList); }
        finally { btnCancel.interactable = true; _navigating = false; }
    }

    async Task SetupEditSelectionAsync(string companyId, string jobId)
    {
        _suppress = true;

        // 회사 드롭다운을 원본 companyId로 맞춤
        SelectDropdown(ddCompany, companyId, _companies.Select(c => c.id).ToArray());
        _model.companyId = companyId;

        // 직무 목록만 로드(0번 선택/OnJobChanged 호출하지 않음)
        await LoadJobsForCompanyOnly(companyId);

        // 직무 드롭다운을 원본 jobId로 맞춤
        SelectDropdown(ddJob, jobId, _jobs.Select(j => j.id).ToArray());
        _model.jobId = jobId;

        _suppress = false;

        // 이제 정확한 직무로 한 번만 질문/답변 UI를 구성(여기서 _prefillAnswers 주입됨)
        await OnJobChanged();
    }

    async Task LoadJobsForCompanyOnly(string companyId)
    {
        ddJob.options.Clear();
        _jobs = Array.Empty<JobRole>();
        if (string.IsNullOrEmpty(companyId)) return;

        _jobs = await _lookup.GetJobsAsync(companyId);
        ddJob.options = _jobs.Select(j => new TMP_Dropdown.OptionData(j.name)).ToList();
    }
}
