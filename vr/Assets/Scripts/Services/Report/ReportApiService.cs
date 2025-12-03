using App.Core;
using App.Infra;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


namespace App.Services
{
    /// <summary>
    /// /api/report/me, /api/report/{reportId} 를 호출하는 실제 API 연동 서비스
    /// 기존 IReportService 시그니처 그대로 구현
    /// </summary>
    public class ReportApiService : IReportService
    {
        // -------- 공통 응답 래퍼(로컬 클래스로 정의: 중복 충돌 방지) --------
        [Serializable]
        private class ApiEnvelope<T>
        {
            public bool success;
            public string message;
            public int code;
            public T data;
        }

        // 목록 API의 data 한 행
        [Serializable]
        private class ReportListRowDto
        {
            public string reportId;
            public string CompanyName;
            public string part;
            public string createdAt;
        }

        // 상세 API의 data 전체(백엔드 응답에 맞춤)
        [Serializable]
        private class ReportDetailApiDto
        {
            public string comment;
            public List<HeartBeatSampleDto> heartBeats;
            public ScoresDto myScores;
            public ScoresDto averageScores;
            public List<QnaItemDto> qnaList;
        }

        public async Task<ReportListItem[]> GetListAsync(string companyFilter, string jobFilter)
        {
            var url = $"{HttpClientBase.BaseUrl}/api/report/me";

            var (status, text, result, error) = await HttpClientBase.GetAuto(url, auth: true);
            if (result != UnityWebRequest.Result.Success || status >= 400)
                throw new Exception($"GET /api/report/me failed {status} {error}\n{text}");

            var env = JsonUtility.FromJson<ApiEnvelope<ReportListRowDto[]>>(text);
            var rows = env?.data ?? Array.Empty<ReportListRowDto>();

            // UI에서 쓰는 모델로 매핑
            var items = rows
                .Where(r =>
                    (string.IsNullOrEmpty(companyFilter) || (r.CompanyName?.IndexOf(companyFilter, StringComparison.OrdinalIgnoreCase) >= 0)) &&
                    (string.IsNullOrEmpty(jobFilter) || (r.part?.IndexOf(jobFilter, StringComparison.OrdinalIgnoreCase) >= 0)))
                .Select(r => new ReportListItem
                {
                    id = r.reportId,
                    companyName = r.CompanyName,
                    jobName = r.part,
                    createdAt = r.createdAt
                })
                .ToArray();

            return items;
        }

        public async Task<ReportDetailDto> GetDetailAsync(string reportId)
        {
            var url = $"{HttpClientBase.BaseUrl}/api/report/{reportId}";
            var (status, text, result, error) =
                await HttpClientBase.GetAuto(url, auth: true);

            if (result != UnityWebRequest.Result.Success || status >= 400)
                throw new Exception($"GET /api/report/{reportId} failed {status} {error}");

            var env = JsonUtility.FromJson<ApiEnvelope<ReportDetailApiDto>>(text);
            var d = env?.data ?? new ReportDetailApiDto
            {
                comment = "",
                heartBeats = new List<HeartBeatSampleDto>(),
                myScores = new ScoresDto(),
                averageScores = new ScoresDto(),
                qnaList = new List<QnaItemDto>()
            };

            // 컨트롤러가 참조하는 DTO로 매핑 (ReportDetailDto는 프로젝트에 이미 존재)
            var dto = new ReportDetailDto
            {
                comment = d.comment,
                heartBeats = d.heartBeats ?? new List<HeartBeatSampleDto>(),
                myScores = d.myScores ?? new ScoresDto(),
                averageScores = d.averageScores ?? new ScoresDto(),
                qnaList = d.qnaList ?? new List<QnaItemDto>()
            };
            return dto;
        }

        public async Task DeleteAsync(string reportId)
        {
            var url = $"{HttpClientBase.BaseUrl}/api/report/{reportId}";
            var (status, text, result, error) = await HttpClientBase.DeleteAuto(url, auth: true);
            if (result != UnityWebRequest.Result.Success || status >= 400)
                throw new Exception($"DELETE /api/report/{reportId} failed {status} {error}\n{text}");
        }
    }
}
