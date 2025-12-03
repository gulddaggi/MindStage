using App.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>더미 자기소개서 서비스 - 메모리 리스트 기반 CRUD.</summary>

namespace App.Services
{
    public class DummyResumeService : IResumeService
    {
        // 목록/상세 메모리 저장소
        readonly List<ResumeListItem> _items = new();
        readonly Dictionary<string, ResumeDetail> _details = new();

        public DummyResumeService()
        {
            // 초기 더미 데이터
            _items.AddRange(new[] {
                new ResumeListItem {
                    id="r1", title="슈퍼센트 클라 지원서",
                    companyId="c1", companyName="슈퍼센트",
                    jobId="j1", jobName="클라이언트",
                    modifiedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    hasQuestionSet=true
                },
                new ResumeListItem {
                    id="r2", title="넥슨 게임클라",
                    companyId="c2", companyName="넥슨",
                    jobId="j3", jobName="게임클라",
                    modifiedAt=DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:mm"),
                    hasQuestionSet=false
                }
            });

            _details["r1"] = new ResumeDetail
            {
                id = "r1",
                companyId = "c1",
                jobId = "j1",
                questions = new[] {
                    new Question{ id="q1", number="1", text="자기소개를 해주세요."},
                    new Question{ id="q2", number="2", text="최근 해결한 기술 문제는?"}
                },
                answers = new List<AnswerItem> {
                    new AnswerItem{ questionId="q1", answer="Unity/VR 프로젝트 PM 겸 프로그래머..."},
                    new AnswerItem{ questionId="q2", answer="XR UI 입력 충돌을 XRI로 정리..."}
                },
            };
        }

        public Task<ResumeListItem[]> GetListAsync(string companyId = null, string jobId = null)
        {
            var q = _items.Where(i =>
                (string.IsNullOrEmpty(companyId) || i.companyId == companyId) &&
                (string.IsNullOrEmpty(jobId) || i.jobId == jobId)).ToArray();
            return Task.FromResult(q);
        }

        /// <summary>상세 조회: 없으면 목록의 회사/직무를 이어받아 시드 생성.</summary>
        public Task<ResumeDetail> GetAsync(string id)
        {
            if (_details.TryGetValue(id, out var d))
                return Task.FromResult(d);

            // 목록에서 해당 항목을 찾아 회사/직무를 보존
            var it = _items.FirstOrDefault(x => x.id == id);
            d = new ResumeDetail
            {
                id = id,
                companyId = it?.companyId,   // <- r2면 "c2"
                jobId = it?.jobId,       // <- r2면 "j3"
                questions = Array.Empty<Question>(),
                answers = new List<AnswerItem>()
            };

            _details[id] = d;               // 캐시에 보관(수정 시 동일 참조)
            return Task.FromResult(d);
        }

        public Task<ResumeDetail> CreateAsync(ResumeDetail p)
        {
            p.id ??= Guid.NewGuid().ToString("N");
            _details[p.id] = p;

            _items.Insert(0, new ResumeListItem
            {
                id = p.id,
                title = p.answers?.FirstOrDefault()?.answer?.Substring(0, Math.Min(12, p.answers[0].answer.Length)) ?? "새 지원서",
                companyId = p.companyId,
                companyName = p.companyId == "c1" ? "슈퍼센트" : "넥슨",
                jobId = p.jobId,
                jobName = p.jobId == "j1" ? "클라이언트" : p.jobId == "j2" ? "서버" : "게임클라",
                modifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                hasQuestionSet = p.questions?.Length > 0
            });
            return Task.FromResult(p);
        }

        public Task<ResumeDetail> UpdateAsync(string id, ResumeDetail p)
        {
            p.id = id;
            _details[id] = p;
            var it = _items.FirstOrDefault(x => x.id == id);
            if (it != null) it.modifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            return Task.FromResult(p);
        }

        public Task<bool> DeleteAsync(string id)
        {
            var removed = _items.RemoveAll(x => x.id == id) > 0;
            _details.Remove(id);
            return Task.FromResult(removed);
        }
    }
}
