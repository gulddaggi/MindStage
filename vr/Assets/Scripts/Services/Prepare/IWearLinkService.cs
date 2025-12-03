using System.Threading.Tasks;
using App.Core;

namespace App.Services
{
    public interface IWearLinkService
    {
        Task<WearLinkStatus> GetStatusAsync();
        Task<WearLinkStatus> RegisterAsync(WearLinkRegisterRequest req); // 연결 시도
        Task UnlinkAsync();                                              // 연결 해제
    }
}
