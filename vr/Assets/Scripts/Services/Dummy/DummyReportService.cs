using App.Core;
using App.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class DummyReportService : IReportService
{
    public Task<ReportListItem[]> GetListAsync(string companyFilter, string jobFilter)
    {
        var all = new[]
        {
            new ReportListItem { id = "demo-001", companyName = "삼성전자", jobName = "SOFTWARE", createdAt = "2025-11-11T10:00:00" },
            new ReportListItem { id = "demo-002", companyName = "현대오토에버", jobName = "SOFTWARE", createdAt = "2025-11-10T14:30:00" },
        };

        var filtered = all.Where(r =>
            (string.IsNullOrEmpty(companyFilter) || r.companyName.IndexOf(companyFilter, StringComparison.OrdinalIgnoreCase) >= 0) &&
            (string.IsNullOrEmpty(jobFilter) || r.jobName.IndexOf(jobFilter, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToArray();

        return Task.FromResult(filtered);
    }

    public Task<ReportDetailDto> GetDetailAsync(string reportId)
    {
        var beats = Enumerable.Range(0, 300)
            .Select(i => new HeartBeatSampleDto
            {
                bpm = 80 + (int)(5 * Math.Sin(i / 10f)),
                measureAt = DateTime.UtcNow.AddSeconds(i).ToString("o")
            })
            .ToList();

        var dto = new ReportDetailDto
        {
            comment = "# 더미 보고서\n\n이건 더미 데이터입니다.",
            heartBeats = beats,
            myScores = new ScoresDto { Job_Competency = 72, Communication = 68, Teamwork_Leadership = 61, Integrity = 80, Adaptability = 66 },
            averageScores = new ScoresDto { Job_Competency = 70, Communication = 70, Teamwork_Leadership = 70, Integrity = 70, Adaptability = 70 },
            qnaList = new List<QnaItemDto>
        {
            new QnaItemDto { question = "자기소개 부탁드립니다.", relatedQuestion = null, answer = "안녕하세요, 더미입니다.", labels = null },
            new QnaItemDto { question = null, relatedQuestion = "방금 답변에서 협업 경험을 구체화해보세요.", answer = "", labels = null },
        }
        };
        return Task.FromResult(dto);
    }

    public Task DeleteAsync(string reportId) => Task.CompletedTask;
}
