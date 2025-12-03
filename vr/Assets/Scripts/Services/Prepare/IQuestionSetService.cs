using System.Threading.Tasks;
using App.Core;

/// <summary>회사-직무별 질문 세트 생성/조회(더미/실서버 공용 인터페이스).</summary>

namespace App.Services
{
    public interface IQuestionSetService
    {
        Task<QuestionSetSummary[]> ListByCompanyJobAsync(string companyId, string jobId);
        Task<QuestionSetDetail> GetByIdAsync(string setId);
        Task<QuestionSetDetail> CreateNewAsync(string companyId, string jobId, string titleHint = null);
        Task DeleteAsync(string setId); // 선택
    }
}
