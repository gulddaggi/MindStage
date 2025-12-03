using System.Threading.Tasks;

namespace App.Auth
{
    /// <summary>인증 처리 결과(성공 여부, 메시지, 액세스 토큰)를 담는 DTO.</summary>
    
    public struct LoginResult
    {
        public bool ok; public string message; public string accessToken;
    }

    /// <summary>인증 서비스 계약. 로그인/회원가입 및 토큰 보관/삭제를 정의.</summary>
    public interface IAuthService
    {
        Task<LoginResult> LoginAsync(string id, string password);
        Task<LoginResult> RegisterAsync(string email, string password, string name);
        Task<(bool ok, bool available, string message)> CheckEmailAvailableAsync(string email);
        Task<(bool ok, string message)> LogoutAsync();
        Task<(bool ok, string accessToken, string message)> RefreshAsync();
        Task<(bool ok, string message)> ChangePasswordAsync(string oldPassword, string newPassword);

        bool HasAccessToken();             // PlayerPrefs 등에 저장된 토큰 존재하는지 여부
        void ClearToken();
    }
}
