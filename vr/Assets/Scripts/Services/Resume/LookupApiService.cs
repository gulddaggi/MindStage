using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Core;
using App.Infra;
using UnityEngine;

/// <summary>
/// 채용 공고 목록을 불러와 회사/직무/질문으로 변환해 제공.
/// 회사 id = companyName, 직무 id = jobPostingId(string)
/// </summary>
namespace App.Services
{
    /// <summary>
    /// 채용 공고 목록을 불러와 회사/직무/질문으로 변환해 제공.
    /// 회사 id = companyName, 직무 id = jobPostingId(string)
    /// </summary>
    public class LookupApiService : ILookupService
    {
        // 원본 캐시 (JobPosting DTO)
        List<JobPostingDto> _cache;

        public async Task<Company[]> GetCompaniesAsync()
        {
            await EnsureCache();
            var list = _cache
                .Select(p => p.companyName)
                .Distinct()
                .OrderBy(n => n)
                .Select(n => new Company { id = n, name = n })
                .ToArray();

            return list;
        }

        public async Task<JobRole[]> GetJobsAsync(string companyId)
        {
            await EnsureCache();
            if (string.IsNullOrEmpty(companyId)) return Array.Empty<JobRole>();

            var list = _cache
                .Where(p => p.companyName == companyId)
                .Select(p => new JobRole
                {
                    id = p.jobPostingId.ToString(), // 등록 시 그대로 사용
                    name = p.part,
                    companyId = companyId
                })
                .ToArray();

            return list;
        }

        public async Task<Question[]> GetQuestionsByJobAsync(string jobId)
        {
            await EnsureCache();
            if (!int.TryParse(jobId, out var jpId)) return Array.Empty<Question>();

            var post = _cache.FirstOrDefault(p => p.jobPostingId == jpId);
            if (post?.questionResponseDtos == null) return Array.Empty<Question>();

            var qs = new List<Question>();
            for (int i = 0; i < post.questionResponseDtos.Count; i++)
            {
                var q = post.questionResponseDtos[i];
                qs.Add(new Question
                {
                    id = q.questionId.ToString(),
                    number = (i + 1).ToString(), // 화면에 인덱스로도 표시 가능
                    text = q.question
                });
            }
            return qs.ToArray();
        }

        // ───────────────────────────────────────────────
        // 내부: /api/JobPosting/list 원본 로드
        // ───────────────────────────────────────────────
        async Task EnsureCache()
        {
            if (_cache != null) return;

            try
            {
                var posts = await HttpClientBase.GetJson<JobPostingDto[]>("/api/JobPosting/list");
                _cache = posts?.ToList() ?? new List<JobPostingDto>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"LookupApiService EnsureCache() 실패: {ex}");
                _cache = new List<JobPostingDto>();
            }
        }

        // 서버 DTO들
        [Serializable]
        class JobPostingDto
        {
            public int jobPostingId;
            public string companyName;
            public string part;
            public List<QuestionResponseDto> questionResponseDtos;
        }

        [Serializable]
        class QuestionResponseDto
        {
            public int questionId;
            public string question;
            public int limit;
        }
    }
}
