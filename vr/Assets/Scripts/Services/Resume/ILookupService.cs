using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Core;
using UnityEngine;


/// <summary>회사/직무/문항 조회용 서비스(드롭다운/문항 표시).</summary>

public interface ILookupService
{
    Task<Company[]> GetCompaniesAsync();
    Task<JobRole[]> GetJobsAsync(string companyId);
    Task<Question[]> GetQuestionsByJobAsync(string jobId);
}
