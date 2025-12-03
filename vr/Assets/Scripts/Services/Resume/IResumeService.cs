using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Core;
using UnityEngine;

/// <summary>자기소개서 CRUD + JD(PDF) 업로드 서비스.</summary>

public interface IResumeService
{
    Task<ResumeListItem[]> GetListAsync(string companyId = null, string jobId = null);
    Task<ResumeDetail> GetAsync(string resumeId);
    Task<ResumeDetail> CreateAsync(ResumeDetail payload);
    Task<ResumeDetail> UpdateAsync(string resumeId, ResumeDetail payload);
    Task<bool> DeleteAsync(string resumeId);
}
