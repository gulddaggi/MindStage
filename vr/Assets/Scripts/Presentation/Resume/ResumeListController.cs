using App.Core;
using App.Infra;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>자기소개서 목록/필터/신규/편집/삭제 제어.</summary>

public class ResumeListController : MonoBehaviour
{
    [Header("Filter")]
    public TMP_Dropdown ddCompany;
    public TMP_Dropdown ddJob;
    public Button btnSearch;
    public Button btnCreate;
    public Button btnBack;

    [Header("Table")]
    public Transform tableContent;      // ScrollView Content
    public GameObject rowPrefab;        // TableRow_Resume.prefab

    ILookupService _lookup;
    IResumeService _resume;

    Company[] _companies;
    JobRole[] _jobs;
    ResumeListItem[] _items;

    bool _navigating;

    [SerializeField]
    UiModeSwitcher uiModeSwitcher;

    async void Start()
    {
        _lookup = App.Infra.Services.Resolve<ILookupService>();
        _resume = App.Infra.Services.Resolve<IResumeService>();

        btnSearch.onClick.AddListener(() => _ = Reload());
        btnCreate.onClick.AddListener(() => _ = OnCreate());
        btnBack.onClick.AddListener(() => _ = SceneLoader.LoadSingleAsync(SceneIds.MainMenu));

        await LoadCompanies();
        await Reload();
    }

    async Task LoadCompanies()
    {
        _companies = await _lookup.GetCompaniesAsync();
        ddCompany.options = _companies.Select(c => new TMP_Dropdown.OptionData(c.name)).ToList();
        ddCompany.onValueChanged.AddListener(async _ =>
        {
            var sel = _companies.ElementAtOrDefault(ddCompany.value)?.id;
            await LoadJobs(sel);
        });
        var firstCompanyId = _companies.ElementAtOrDefault(0)?.id;
        await LoadJobs(firstCompanyId);
    }

    async Task LoadJobs(string companyId)
    {
        ddJob.options.Clear();
        if (string.IsNullOrEmpty(companyId)) return;
        _jobs = await _lookup.GetJobsAsync(companyId);
        ddJob.options = _jobs.Select(j => new TMP_Dropdown.OptionData(j.name)).ToList();
    }

    async Task Reload()
    {
        var companyId = _companies?.ElementAtOrDefault(ddCompany.value)?.id;
        // 중요: 목록 응답에는 jobPostingId가 없으므로 "직무 이름(part)"로 필터하도록 넘김
        var jobName = _jobs?.ElementAtOrDefault(ddJob.value)?.name;

        _items = await _resume.GetListAsync(companyId, jobName) ?? Array.Empty<ResumeListItem>();
        BuildTable(_items);
    }

    void BuildTable(ResumeListItem[] items)
    {
        // 기존 행 제거
        for (int i = tableContent.childCount - 1; i >= 0; i--)
            Destroy(tableContent.GetChild(i).gameObject);

        foreach (var it in items)
        {
            var row = Instantiate(rowPrefab, tableContent).transform;

            var tTitle = row.Find("Title")?.GetComponent<TMPro.TMP_Text>();
            var tCompany = row.Find("Company")?.GetComponent<TMPro.TMP_Text>();
            var tJob = row.Find("Job")?.GetComponent<TMPro.TMP_Text>();
            var tMod = row.Find("Modified")?.GetComponent<TMPro.TMP_Text>();
            var tBadge = row.Find("Badge")?.GetComponent<TMPro.TMP_Text>();
            var btnOpen = row.Find("BtnOpen")?.GetComponent<Button>();
            var btnDel = row.Find("BtnDelete")?.GetComponent<Button>();

            if (tTitle) tTitle.text = it.title ?? $"{it.companyName} / {it.jobName}";
            if (tCompany) tCompany.text = it.companyName ?? "-";
            if (tJob) tJob.text = it.jobName ?? "-";
            if (tMod) tMod.text = FormatDate(it.modifiedAt);
            if (tBadge) tBadge.text = MapProgressBadge(it.progressStatus);


            if (btnOpen != null)
            {
                btnOpen.onClick.RemoveAllListeners();
                btnOpen.onClick.AddListener(() =>
                {
                    // 편집 제거 → '조회 전용'으로 동일 씬을 재사용하거나, 필요 시 별도 조회 씬으로 변경
                    PlayerPrefs.SetString("resume.edit.id", it.id);
                    _ = SceneLoader.LoadSingleAsync(SceneIds.ResumeEdit);
                });
            }

            
            if (btnDel != null)
            {
                btnDel.onClick.RemoveAllListeners();
                btnDel.onClick.AddListener(async () =>
                {
                    var ok = await _resume.DeleteAsync(it.id);
                    if (ok) await Reload();
                });
            }
            
        }
    }

    static string FormatDate(string iso)
    {
        if (DateTime.TryParse(iso, out var dt))
            return dt.ToString("yyyy-MM-dd HH:mm");
        return iso ?? "-";
    }

    Task OnCreate()
    {
        if (_navigating) return Task.CompletedTask;
        _navigating = true;
        uiModeSwitcher?.SwitchToDesktop();
        return SceneLoader.LoadSingleAsync(SceneIds.ResumeEdit);
    }

    static string MapProgressBadge(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "CREATING": return "질문 생성 중";
            case "NOT_STARTED": return "면접 가능";
            case "IN_PROGRESS": return "면접 진행 중";
            case "COMPLETED": return "레포트 생성 중";
            case "REPORTED": return "레포트 완료";
            case "FAILED": return "생성 실패";
            default: return "-";
        }
    }
}