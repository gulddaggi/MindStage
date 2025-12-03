using App.Core;
using App.Infra;
using App.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using static System.Net.WebRequestMethods;

/// <summary>자기소개서 CRUD API 구현.</summary>

public class ResumeApiService : IResumeService
{
    private static string Base => HttpClientBase.BaseUrl;

    // 목록
    public async Task<ResumeListItem[]> GetListAsync(string companyId = null, string jobIdOrName = null)
    {
        // jobIdOrName: 직무 이름(part)로도 들어올 수 있게 처리
        ResumeMeItemDto[] raw = Array.Empty<ResumeMeItemDto>();
        try
        {
            raw = await HttpClientBase.GetJson<ResumeMeItemDto[]>("/api/resume/me") ?? Array.Empty<ResumeMeItemDto>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"ResumeApiService.GetListAsync() 실패: {ex}");
        }

        IEnumerable<ResumeMeItemDto> q = raw;

        if (!string.IsNullOrEmpty(companyId))
            q = q.Where(x => x.companyName == companyId);

        if (!string.IsNullOrEmpty(jobIdOrName))
        {
            // 숫자가 아니면 직무 이름(part)로 간주하여 필터
            if (!int.TryParse(jobIdOrName, out _))
                q = q.Where(x => x.part == jobIdOrName);
        }

        var list = q.Select(x => new ResumeListItem
        {
            id = x.resumeId.ToString(),
            title = $"{x.companyName} / {x.part}",
            companyId = x.companyName,
            companyName = x.companyName,
            jobId = null,                 // 목록 응답에 없음
            jobName = x.part,
            modifiedAt = x.createdAt,     // 생성일을 표시 컬럼에 사용
            hasQuestionSet = true,
            interviewId = x.interviewId > 0 ? x.interviewId.ToString() : null,
            progressStatus = x.progressStatus
        })
        .ToArray();

        return list;
    }

    public async Task<ResumeListItem[]> GetJobPostingsAsync()
    {
        JobPostingDto[] rawList = Array.Empty<JobPostingDto>();

        try
        {
            var response = await HttpClientBase.GetJson<JobPostingDto[]>("/api/JobPosting/list");

            if (response == null)
            {
                Debug.LogError("[API] 응답이 NULL입니다.");
            }
            else
            {
                rawList = response;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"GetJobPostingsAsync 실패: {ex}");
        }

        // 변환 로직
        var result = rawList.Select(x => new ResumeListItem
        {
            id = x.jobPostingId.ToString(),
            title = $"{x.companyName} / {x.part}",
            companyName = x.companyName,
            jobName = x.part,
            modifiedAt = null,
            progressStatus = "NOT_STARTED",
            interviewId = null
        })
        .ToArray();

        return result;
    }

    public async Task<DemoInterviewResponse> CreateDemoInterviewAsync(int jobPostingId)
    {
        var url = $"/api/Interview/demo/create?jobPostingId={jobPostingId}";

        try
        {
            // POST 요청
            var (status, text, _, error) = await HttpClientBase.PostJsonAuto(
                 HttpClientBase.BaseUrl + url, 
                 "{}",
                 auth: true
            );

            if (status >= 200 && status < 300)
            {
                var res = JsonUtility.FromJson<ApiResponse<DemoInterviewResponse>>(text);
                if (res != null && res.success)
                {
                    return res.data;
                }
            }

            Debug.LogError($"데모 면접 생성 실패 {status}: {error}\n{text}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"CreateDemoInterviewAsync 예외: {ex}");
            return null;
        }
    }

    public async Task<ResumeDetail> GetAsync(string resumeId)
    {
        // 1) 서버에서 상세 받아오기 (data만 T로 역직렬화)
        var dto = await HttpClientBase.GetJson<ResumeDetailDto>($"/api/resume/{resumeId}");
        if (dto == null) return null;

        // 2) 기존 화면 모델로 매핑 (질문/답배열 구성)
        var questions = new List<Question>();
        var answers = new List<AnswerItem>();
        for (int i = 0; i < (dto.answers?.Length ?? 0); i++)
        {
            var qa = dto.answers[i];
            var qid = (i + 1).ToString(); // 로컬 표시용 가상 questionId
            questions.Add(new Question { id = qid, number = qid, text = qa.question });

            string restoredAnswer = UnsanitizeFromJson(qa.answer);

            answers.Add(new AnswerItem { questionId = qid, answer = restoredAnswer });
        }

        return new ResumeDetail
        {
            id = resumeId,
            questions = questions.ToArray(),
            answers = answers
        };

        string UnsanitizeFromJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            // 저장할 때 변환했던 \\n, \\t를 다시 원래대로 복구
            return raw.Replace("\\n", "\n").Replace("\\t", "\t");
        }
    }

    // 등록
    public async Task<ResumeDetail> CreateAsync(ResumeDetail payload)
    {
        if (payload == null) return null;
        if (!int.TryParse(payload.jobId, out var jobPostingId))
            throw new Exception("jobPostingId 없음(직무 선택 필요)");

        var body = new RegisterResumeReq
        {
            jobPostingId = jobPostingId,
            answerRequestDtos = (payload.answers ?? new List<AnswerItem>())
                .Select(a => new AnswerReq
                {
                    questionId = int.TryParse(a.questionId, out var qid) ? qid : 0,
                    content = SanitizeForJson(a.answer)
                })
                .ToList()
        };

        string SanitizeForJson(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            // 줄바꿈 통일 후 \n 텍스트로 이스케이프
            return raw
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        try
        {
            // 서버는 data가 없을 수도 있으므로 Auto로 처리
            var json = JsonUtility.ToJson(body);
            var (status, text, _, error) = await HttpClientBase.PostJsonAuto(
                HttpClientBase.BaseUrl + "/api/resume/register", json, auth: true);

            if (status is >= 200 and < 300)
                return payload; // 성공: 그대로 반환(필요 시 후처리)

            Debug.LogError($"Resume 등록 실패 HTTP {status}: {error}\n{text}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"ResumeApiService.CreateAsync() 예외: {ex}");
            return null;
        }
    }

    public Task<ResumeDetail> UpdateAsync(string resumeId, ResumeDetail payload)
        => throw new NotSupportedException("자기소개서 편집은 비활성화됨");

    public async Task<bool> DeleteAsync(string resumeId)
    {
        try
        {
            return await HttpClientBase.Delete($"/api/resume/{resumeId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Resume 삭제 실패: {ex}");
            return false;
        }
    }

    // ───────── 서버 DTO ─────────
    [Serializable]
    class ResumeMeItemDto
    {
        public int resumeId;
        public string companyName;
        public string part;
        public string createdAt; // ISO-8601
        public int interviewId;
        public string progressStatus; // CREATING/NOT_STARTED/IN_PROGRESS/COMPLETED/REPORTED/FAILED
    }

    [Serializable]
    class RegisterResumeReq
    {
        public int jobPostingId;
        public List<AnswerReq> answerRequestDtos;
    }

    [Serializable]
    class AnswerReq
    {
        public int questionId;
        public string content;
    }

    [Serializable]
    class ApiResponse<T>
    {
        public bool success;
        public string message;
        public string code;
        public T data;
    }

    // 채용 공고 DTO
    [Serializable]
    public class JobPostingDto
    {
        public long jobPostingId;
        public string companyName;
        public string part;
    }

    // 데모 면접 생성 응답 DTO
    [Serializable]
    public class DemoInterviewResponse
    {
        public int interviewId;
        public int questionId;
        public string questionPresignedUrl;
        public string exampleIntroduction;
    }
}
