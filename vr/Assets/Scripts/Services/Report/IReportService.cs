using App.Core;
using App.Services;
using System.Threading.Tasks;

public interface IReportService
{
    Task<ReportListItem[]> GetListAsync(string companyId, string jobId);
    Task<ReportDetailDto> GetDetailAsync(string reportId);
    Task DeleteAsync(string reportId);
}
