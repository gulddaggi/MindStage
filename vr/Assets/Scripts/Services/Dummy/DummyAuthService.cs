using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace App.Auth
{
    /// <summary>MVP용 인메모리 더미 인증 구현. 실제 백엔드 연동 전까지 임시 사용.</summary>

    public class DummyAuthService : IAuthService
    {
        const string KEY_ACCESS = "auth.accessToken";
        const string KEY_REFRESH = "auth.refreshToken";
        readonly Dictionary<string, string> _users = new(); // id->pw

        public Task<LoginResult> RegisterAsync(string email, string pw, string name)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pw))
                return Task.FromResult(new LoginResult { ok = false, message = "이메일/비밀번호를 입력하세요" });

            if (_users.ContainsKey(email))
                return Task.FromResult(new LoginResult { ok = false, message = "이미 존재하는 이메일입니다." });

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return Task.FromResult(new LoginResult { ok = false, message = "올바른 이메일 형식이 아닙니다." });

            _users[email] = pw;
            return Task.FromResult(new LoginResult { ok = true, message = "가입 완료" });
        }

        public Task<LoginResult> LoginAsync(string email, string pw)
        {
            if (!_users.TryGetValue(email, out var saved) || saved != pw)
                return Task.FromResult(new LoginResult { ok = false, message = "이메일 또는 비밀번호가 올바르지 않습니다" });

            var at = "dummy_access_token";
            var rt = "dummy_refresh_token";
            PlayerPrefs.SetString(KEY_ACCESS, at);
            PlayerPrefs.SetString(KEY_REFRESH, rt);
            PlayerPrefs.Save();
            return Task.FromResult(new LoginResult { ok = true, accessToken = at, message = "로그인 성공" });
        }

        public Task<(bool ok, bool available, string message)> CheckEmailAvailableAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Task.FromResult((false, false, "이메일을 입력하세요."));

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return Task.FromResult((false, false, "올바른 이메일 형식이 아닙니다."));

            bool available = !_users.ContainsKey(email);
            string msg = available ? "사용 가능한 이메일입니다." : "이미 사용 중인 이메일입니다.";
            return Task.FromResult((true, available, msg));
        }

        public Task<(bool ok, string message)> LogoutAsync()
        {
            ClearToken();
            return Task.FromResult((true, "로그아웃 완료(더미)"));
        }

        public Task<(bool ok, string accessToken, string message)> RefreshAsync()
        {
            if (!PlayerPrefs.HasKey(KEY_REFRESH))
                return Task.FromResult<(bool, string, string)>(
                    (false, null, "리프레시 토큰이 없습니다.(더미)")
                );

            var newAt = "dummy_access_token_refreshed";
            PlayerPrefs.SetString(KEY_ACCESS, newAt);
            PlayerPrefs.Save();

            return Task.FromResult<(bool, string, string)>(
                (true, newAt, "토큰 재발급 완료(더미)")
            );
        }

        public Task<(bool ok, string message)> ChangePasswordAsync(string oldPw, string newPw)
        {
            // 마지막 로그인 이메일을 저장하지 않는 구조라면,
            // 테스트용으로 pwHash만 비교/교체
            var oldHash = PlayerPrefs.GetString("auth.pwHash", "");
            bool ok = oldHash == HashPw(oldPw);
            if (ok)
            {
                PlayerPrefs.SetString("auth.pwHash", HashPw(newPw));
                PlayerPrefs.Save();
                return Task.FromResult((true, "비밀번호 변경 완료(더미)"));
            }
            return Task.FromResult((false, "현재 비밀번호가 일치하지 않습니다(더미)"));
        }
        static string HashPw(string s)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var b = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s ?? ""));
            var sb = new System.Text.StringBuilder();
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        public bool HasAccessToken() => PlayerPrefs.HasKey(KEY_ACCESS);
        public void ClearToken()
        {
            PlayerPrefs.DeleteKey(KEY_ACCESS);
            PlayerPrefs.DeleteKey(KEY_REFRESH);
            PlayerPrefs.Save();
        }
    }
}
