using System.Linq;
using System.Threading.Tasks;
using App.Core;

/// <summary>더미 조회 서비스(회사/직무/문항) - 에디터/무백엔드 테스트용.</summary>

namespace App.Services
{
    public class DummyLookupService : ILookupService
    {
        static readonly Company[] Companies = {
            new Company{ id="c1", name="슈퍼센트"},
            new Company{ id="c2", name="넥슨"}
        };

        static readonly JobRole[] Jobs = {
            new JobRole{ id="j1", name="클라이언트", companyId="c1"},
            new JobRole{ id="j2", name="서버",     companyId="c1"},
            new JobRole{ id="j3", name="게임클라",  companyId="c2"},
        };

        static readonly Question[] Q_c1j1 = {
            new Question{ id="q1", number="1", text="자기소개를 해주세요."},
            new Question{ id="q2", number="2", text="최근 해결한 기술 문제는?"}
        };
        static readonly Question[] Q_c1j2 = {
            new Question{ id="q3", number="1", text="대용량 트래픽 대응 경험은?"},
        };
        static readonly Question[] Q_c2j3 = {
            new Question{ id="q4", number="1", text="UI/UX 품질 개선 사례는?"}
        };

        public Task<Company[]> GetCompaniesAsync() => Task.FromResult(Companies);
        public Task<JobRole[]> GetJobsAsync(string companyId)
            => Task.FromResult(Jobs.Where(j => j.companyId == companyId).ToArray());
        public Task<Question[]> GetQuestionsByJobAsync(string jobId)
            => Task.FromResult(jobId switch
            {
                "j1" => Q_c1j1,
                "j2" => Q_c1j2,
                "j3" => Q_c2j3,
                _ => new Question[0]
            });
    }
}
