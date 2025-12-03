using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using App.Core;
using App.Infra;
using App.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultsListController : MonoBehaviour
{
    [Header("Filter")]
    public TMP_Dropdown ddCompany;
    public TMP_Dropdown ddJob;
    public Button btnSearch;
    public Button btnBack;

    [Header("Table")]
    public Transform tableContent;
    public GameObject rowPrefab;
    public GameObject emptyView; // "아직 결과가 없습니다"

    ILookupService _lookup;
    IReportService _reports;
    Company[] _companies;
    JobRole[] _jobs;
    ReportListItem[] _items;

    bool _busyNav;

    async void Start()
    {
        _lookup = Services.Resolve<ILookupService>();
        _reports = Services.Resolve<IReportService>();

        btnSearch.onClick.AddListener(() => _ = Reload());
        btnBack.onClick.AddListener(() => _ = App.Infra.SceneLoader.LoadSingleAsync(SceneIds.MainMenu));

        await LoadCompanies();
        await Reload();
    }

    async Task LoadCompanies()
    {
        _companies = await _lookup.GetCompaniesAsync();

        var opts = new List<TMP_Dropdown.OptionData>();
        opts.Add(new TMP_Dropdown.OptionData("전체"));
        opts.AddRange(_companies.Select(c => new TMP_Dropdown.OptionData(c.name)));

        ddCompany.options = opts;
        ddCompany.value = 0; // 전체
        ddCompany.RefreshShownValue();

        ddCompany.onValueChanged.AddListener(async _ => {
            await LoadJobs(SelectedCompanyId()); // 아래 헬퍼 사용
        });

        await LoadJobs(SelectedCompanyId());
    }

    async Task LoadJobs(string companyId)
    {
        ddJob.options.Clear();

        if (string.IsNullOrEmpty(companyId))
        {
            _jobs = Array.Empty<JobRole>();
            ddJob.options = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData("전체") };
            ddJob.value = 0;
            ddJob.RefreshShownValue();
            return;
        }

        _jobs = await _lookup.GetJobsAsync(companyId);

        var jobOpts = new List<TMP_Dropdown.OptionData>();
        jobOpts.Add(new TMP_Dropdown.OptionData("전체"));
        jobOpts.AddRange(_jobs.Select(j => new TMP_Dropdown.OptionData(j.name)));

        ddJob.options = jobOpts;
        ddJob.value = 0; // 전체
        ddJob.RefreshShownValue();
    }

    async Task Reload()
    {
        var companyId = SelectedCompanyId();
        var jobId = SelectedJobId();

        _items = await _reports.GetListAsync(companyId, jobId);
        BuildTable(_items);
    }

    string SelectedCompanyId()
    {
        // 0 = 전체
        if (ddCompany.value <= 0) return null;
        return _companies?.ElementAtOrDefault(ddCompany.value - 1)?.id;
    }

    string SelectedJobId()
    {
        // 0 = 전체
        if (ddJob.value <= 0) return null;
        return _jobs?.ElementAtOrDefault(ddJob.value - 1)?.id;
    }

    void BuildTable(ReportListItem[] items)
    {
        for (int i = tableContent.childCount - 1; i >= 0; i--) Destroy(tableContent.GetChild(i).gameObject);
        emptyView?.SetActive(items.Length == 0);

        foreach (var it in items)
        {
            var row = Instantiate(rowPrefab, tableContent).transform;
            row.Find("Company")?.GetComponent<TMP_Text>()?.SetText(it.companyName ?? "");
            row.Find("Job")?.GetComponent<TMP_Text>()?.SetText(it.jobName ?? "");
            row.Find("Date")?.GetComponent<TMP_Text>()?.SetText(FormatCreatedAt(it.createdAt));

            var tBadge = row.Find("Badge")?.GetComponent<TMP_Text>();
            if (tBadge)
            {
                tBadge.text = "생성됨";
                tBadge.color = new Color(0.1f, 0.6f, 0.2f);
            }

            row.Find("BtnDetail")?.GetComponent<Button>()?.onClick.AddListener(() => OnDetail(it));
            row.Find("BtnDelete")?.GetComponent<Button>()?.onClick.AddListener(() => _ = OnDelete(it.id));
        }
    }

    static string FormatCreatedAt(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        // 파싱 성공 시 원하는 포맷으로
        if (DateTime.TryParse(raw, out var dt))
            return dt.ToString("yyyy-MM-dd HH:mm");

        // 실패 시 T 기준으로 잘라서 fallback
        int t = raw.IndexOf('T');
        if (t >= 0 && raw.Length >= t + 6)
            return $"{raw.Substring(0, 10)} {raw.Substring(t + 1, 5)}";

        return raw;
    }

    async void OnDetail(ReportListItem item)
    {
        if (_busyNav) return; _busyNav = true;
        try
        {
            PlayerPrefs.SetString("report.detail.id", item.id);
            PlayerPrefs.SetString("report.detail.company", item.companyName ?? "");
            PlayerPrefs.SetString("report.detail.job", item.jobName ?? "");
            PlayerPrefs.SetString("report.detail.datetime", item.createdAt ?? "");
            PlayerPrefs.Save();
            await SceneLoader.LoadSingleAsync(SceneIds.ReportDetail);
        }
        catch (Exception e) { Debug.LogException(e); }
        finally { _busyNav = false; }
    }

    async Task OnDelete(string id)
    {
        try
        {
            await _reports.DeleteAsync(id);
            await Reload();
        }
        catch (Exception e) { Debug.LogException(e); }
    }
}
