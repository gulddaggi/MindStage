using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Core;
using App.Infra;
using ServiceHub = App.Infra.Services;
using UnityEngine;

/// <summary>PlayerPrefs(JSON)에 세트를 저장하고 회사-직무별 순번을 증가시키는 더미 구현.</summary>

namespace App.Services
{
    public class DummyQuestionSetService : IQuestionSetService
    {
        const string KEY = "qset.store";

        QSetSave _cache;
        ILookupService _lookup;
        List<QuestionSetDetail> _records;
        List<QSetSeqCounter> _counters;

        public DummyQuestionSetService()
        {
            _lookup = ServiceHub.Resolve<ILookupService>();
            _cache = Load();
        }

        public async Task<QuestionSetSummary[]> ListByCompanyJobAsync(string companyId, string jobId)
        {
            // 요약: id/회사명/직무명/생성시간만
            var list = Records()
                .Where(r => NameToId(companyId, r.companyName) && NameToId(jobId, r.jobName)) // 이름→id 비교가 필요 없으면 이 줄을 r.companyName/jobName 비교로 바꿔도 OK
                .OrderBy(r => r.createdAtIso)
                .Select(r => new QuestionSetSummary
                {
                    id = r.id,
                    companyName = r.companyName,
                    jobName = r.jobName,
                    createdAtIso = r.createdAtIso
                })
                .ToArray();

            // 위 필터가 불편하면 companyId/jobId 대신 companyName/jobName 기준으로 ListByCompanyJobAsync 시그니처를 바꾸는 것도 방법.
            return await Task.FromResult(list);
        }

        public Task<QuestionSetDetail> GetByIdAsync(string setId)
        {
            var r = Records().FirstOrDefault(x => x.id == setId);
            return Task.FromResult(r);
        }

        public async Task<QuestionSetDetail> CreateNewAsync(string companyId, string jobId, string titleHint = null)
        {
            // 1) 표시용 회사명/직무명 확보
            var companyName = (await _lookup.GetCompaniesAsync())
                                ?.FirstOrDefault(c => c.id == companyId)?.name ?? companyId;
            var jobName = (await _lookup.GetJobsAsync(companyId))
                                ?.FirstOrDefault(j => j.id == jobId)?.name ?? jobId;

            // 2) 현재 직무 질문 스냅샷(InterviewQuestion로 매핑, tags는 빈 배열)
            var src = await _lookup.GetQuestionsByJobAsync(jobId); // Question[]
            var qs = (src == null)
                ? new InterviewQuestion[0]
                : src.Select(q => new InterviewQuestion { id = q.id, text = q.text, tags = System.Array.Empty<string>() })
                     .ToArray();

            // 3) 회사-직무별 시퀀스 증가
            var ctrs = Counters();
            var ctr = ctrs.FirstOrDefault(c => c.companyId == companyId && c.jobId == jobId);
            if (ctr == null) { ctr = new QSetSeqCounter { companyId = companyId, jobId = jobId, nextSeq = 1 }; ctrs.Add(ctr); }
            int seq = ctr.nextSeq++;
            Save(); // 카운터 저장

            // 4) 세트 ID: <companyId>_<jobId>_S###  (예: C01_J02_S003)
            var id = $"{companyId}_{jobId}_S{seq:D3}";

            // 5) 레코드 저장(표시필드만 보관)
            var rec = new QuestionSetDetail
            {
                id = id,
                companyName = companyName,
                jobName = jobName,
                createdAtIso = System.DateTime.UtcNow.ToString("o"),
                items = qs
            };

            var list = Records(); list.Add(rec); Save();
            return rec;
        }

        public Task DeleteAsync(string setId)
        {
            var list = Records();
            int idx = list.FindIndex(r => r.id == setId);
            if (idx >= 0) { list.RemoveAt(idx); Save(); }
            return Task.CompletedTask;
        }

        // ---------- 내부 저장/유틸 ----------
        QSetSave Load()
        {
            var json = PlayerPrefs.GetString(KEY, "");
            if (string.IsNullOrEmpty(json)) return new QSetSave { records = new QuestionSetDetail[0], counters = new QSetSeqCounter[0] };
            try { return JsonUtility.FromJson<QSetSave>(json) ?? new QSetSave { records = new QuestionSetDetail[0], counters = new QSetSeqCounter[0] }; }
            catch { return new QSetSave { records = new QuestionSetDetail[0], counters = new QSetSeqCounter[0] }; }
        }

        void Save()
        {
            _cache.records = _records?.ToArray() ?? _cache.records;
            _cache.counters = _counters?.ToArray() ?? _cache.counters;
            var json = JsonUtility.ToJson(_cache);
            PlayerPrefs.SetString(KEY, json); PlayerPrefs.Save();
        }

        List<QuestionSetDetail> Records() => _records ??= (_cache.records != null ? new List<QuestionSetDetail>(_cache.records) : new List<QuestionSetDetail>());
        List<QSetSeqCounter> Counters() => _counters ??= (_cache.counters != null ? new List<QSetSeqCounter>(_cache.counters) : new List<QSetSeqCounter>());

        // 필요 시 id↔name 비교 유틸(더미에서는 간단히 통과하도록 true 반환해도 무방)
        bool NameToId(string id, string name) => true;
    }
}
