using App.Infra;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace App.Auth
{
    public class AuthHttpService : IAuthService
    {
        private readonly string _base;
        public AuthHttpService(string baseUrl) => _base = baseUrl.TrimEnd('/');

        // === DTOs (OpenAPI에 맞춤) ===
        [Serializable] class SignUpReq { public string email; public string password; public string name; }
        [Serializable] class UserResponseDto { public long userId; public string email; public string name; public string role; }
        [Serializable] class ApiResult { public bool success; public string message; public int code; public string data; }

        // 로그인 DTO
        [Serializable] class LoginReq { public string email; public string password; }
        [Serializable]
        class TokenResponseDto
        {
            public long userId;
            public string email;
            public string name;
            public string role;
            public string accessToken;
            public string refreshToken;
        }

        [Serializable]
        class ChangePwReq
        {
            public string oldPassword;
            public string newPassword;
        }

        const string KEY_ACCESS = "auth.accessToken";
        const string KEY_REFRESH = "auth.refreshToken";

        // ─────────────────────────────────────────
        // 회원가입: POST /api/auth/signUp
        // ─────────────────────────────────────────
        public async Task<LoginResult> RegisterAsync(string email, string password, string name)
        {
            var url = $"{_base}/api/auth/signUp";
            var body = JsonUtility.ToJson(new SignUpReq { email = email, password = password, name = name });

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.SetRequestHeader("Content-Type", "application/json");
            // 회원가입은 토큰 불필요. Authorization 헤더 미설정
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            // 네트워크 에러
            if (req.result != UnityWebRequest.Result.Success)
            {
                return new LoginResult { ok = false, message = $"HTTP {(long)req.responseCode}: {req.error}", accessToken = null };
            }

            // 상태코드 분기
            var status = (int)req.responseCode;
            var text = req.downloadHandler?.text ?? "";
            if (status >= 200 && status < 300)
            {
                // 성공: UserResponseDto
                // (서버 메시지가 없다면 기본 메시지)
                var _ = JsonUtility.FromJson<UserResponseDto>(text); // 필요시 사용
                return new LoginResult { ok = true, message = "회원가입이 완료되었습니다.", accessToken = null };
            }
            else
            {
                // 실패: ApiResult 형태 기대
                string msg = $"가입 실패 (HTTP {status})";
                try
                {
                    var ar = JsonUtility.FromJson<ApiResult>(text);
                    if (!string.IsNullOrWhiteSpace(ar?.message)) msg = ar.message;
                }
                catch { /* ignore */ }

                return new LoginResult { ok = false, message = msg, accessToken = null };
            }
        }

        // ─────────────────────────────────────────
        // 로그인: POST /api/auth/login
        // ─────────────────────────────────────────
        public async Task<LoginResult> LoginAsync(string id, string password)
        {
            var url = $"{_base}/api/auth/login";
            var body = JsonUtility.ToJson(new LoginReq { email = id?.Trim(), password = password });

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                return new LoginResult { ok = false, message = "서버와 연결할 수 없습니다. 인터넷 상태를 확인해주세요." };
            }


            var status = (int)req.responseCode;
            var text = req.downloadHandler?.text?.Trim() ?? "";
            var ctype = req.GetResponseHeader("Content-Type") ?? "";

            // 200번대 성공 처리 (기존 코드 유지)
            if (status is >= 200 and < 300)
            {
                string access = null, refresh = null;

                if (text.Length > 0 && (ctype.Contains("json") || text.StartsWith("{") || text.StartsWith("[")))
                {
                    try
                    {
                        var tok = JsonUtility.FromJson<TokenResponseDto>(text);
                        if (tok != null)
                        {
                            access = string.IsNullOrEmpty(tok.accessToken) ? null : tok.accessToken;
                            refresh = string.IsNullOrEmpty(tok.refreshToken) ? null : tok.refreshToken;
                        }
                    }
                    catch { }

                    if (access == null)
                    {
                        access = ExtractStringValue(text, "accessToken", "access_token", "token", "access");
                        refresh = ExtractStringValue(text, "refreshToken", "refresh_token", "refresh");
                    }
                }

                var setCookie = req.GetResponseHeader("Set-Cookie") ?? "";
                if (string.IsNullOrEmpty(access)) access = ExtractCookie(setCookie, "accessToken");
                if (string.IsNullOrEmpty(refresh)) refresh = ExtractCookie(setCookie, "refreshToken");

                if (!string.IsNullOrEmpty(access) || !string.IsNullOrEmpty(refresh))
                {
                    if (!string.IsNullOrEmpty(access)) PlayerPrefs.SetString(KEY_ACCESS, access);
                    if (!string.IsNullOrEmpty(refresh)) PlayerPrefs.SetString(KEY_REFRESH, refresh);
                    PlayerPrefs.SetString("auth.pwHash", HashPw(password));
                    PlayerPrefs.Save();

                    return new LoginResult { ok = true, message = "로그인 성공", accessToken = access };
                }

                Debug.LogWarning($"[Auth] Login 2xx but no token. Status: {status}");
                return new LoginResult { ok = false, message = "로그인 처리 중 오류가 발생했습니다." };
            }
            else
            {
                string msg = "";

                try
                {
                    if (!string.IsNullOrEmpty(text) && text.StartsWith("{"))
                    {
                        var ar = JsonUtility.FromJson<ApiResult>(text);
                        if (!string.IsNullOrWhiteSpace(ar?.message))
                        {
                            msg = ar.message; // 서버 메시지 우선 사용
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(msg))
                {
                    msg = GetFriendlyErrorMessage(status);
                }

                return new LoginResult { ok = false, message = msg };
            }
        }

        public bool HasAccessToken() => PlayerPrefs.HasKey(KEY_ACCESS);
        public void ClearToken()
        {
            PlayerPrefs.DeleteKey(KEY_ACCESS);
            PlayerPrefs.DeleteKey(KEY_REFRESH);
            PlayerPrefs.Save();
        }

        public async Task<(bool ok, bool available, string message)> CheckEmailAvailableAsync(string email)
        {
            var url = $"{_base}/api/auth/check-email?email={UnityWebRequest.EscapeURL(email ?? string.Empty)}";

            using var req = UnityWebRequest.Get(url);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Accept", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                return (false, false, $"HTTP {(long)req.responseCode}: {req.error}");

            var status = (int)req.responseCode;
            var raw = req.downloadHandler?.text ?? "";
            var text = raw.Trim();

            if (status is >= 200 and < 300)
            {
                // 1) 순수 boolean or "true"/"false"
                string s = text;
                if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
                    s = s.Substring(1, s.Length - 2);

                if (bool.TryParse(s, out bool available1))
                    return (true, available1, available1 ? "사용 가능한 이메일입니다." : "이미 사용 중인 이메일입니다.");

                // 2) JSON 객체 안에서 available/data/result/value 키를 찾아 boolean 파싱
                if (s.StartsWith("{"))
                {
                    // ex) {"available":true} / {"data":false} / {"result":"true"} / {"value":"false"}
                    var m = Regex.Match(
                        s,
                        @"(?:""available""|""data""|""result""|""value"")\s*:\s*(true|false|""true""|""false"")",
                        RegexOptions.IgnoreCase
                    );
                    if (m.Success)
                    {
                        var v = m.Groups[1].Value.Trim('"');
                        bool av = string.Equals(v, "true", System.StringComparison.OrdinalIgnoreCase);
                        return (true, av, av ? "사용 가능한 이메일입니다." : "이미 사용 중인 이메일입니다.");
                    }
                }

                Debug.LogWarning($"[Auth] check-email 200 but unparsable payload: {text}");
                return (false, false, "서버 응답 형식을 해석할 수 없습니다.");
            }

            // 400 등 오류: ApiResult(message) 기대
            try
            {
                var ar = JsonUtility.FromJson<ApiResult>(text);
                if (!string.IsNullOrWhiteSpace(ar?.message))
                    return (false, false, ar.message);
            }
            catch { /* ignore */ }

            return (false, false, $"요청 실패 (HTTP {status})");
        }

        static string ExtractStringValue(string json, params string[] keys)
        {
            if (string.IsNullOrEmpty(json)) return null;
            // "key" : "value"  또는  "key":value (따옴표 없는 케이스도 허용)
            var pattern = "\"({KEYS})\"\\s*:\\s*\"([^\"]+)\"|\"({KEYS})\"\\s*:\\s*(\\S+)";
            var joined = string.Join("|", keys);
            var rx = new Regex(pattern.Replace("{KEYS}", joined), RegexOptions.IgnoreCase);
            var m = rx.Match(json);
            if (!m.Success) return null;
            // 그룹2(따옴표 값) 또는 그룹4(비문자열 값) 중 존재하는 쪽
            var val = m.Groups[2].Success ? m.Groups[2].Value : (m.Groups[4].Success ? m.Groups[4].Value : null);
            if (string.IsNullOrEmpty(val)) return null;
            // true/false 같은 리터럴은 문자열로 쓰지 않도록
            if (string.Equals(val, "null", StringComparison.OrdinalIgnoreCase)) return null;
            if (val.Equals("true", StringComparison.OrdinalIgnoreCase) || val.Equals("false", StringComparison.OrdinalIgnoreCase)) return null;
            return val.Trim().Trim('"');
        }

        static string ExtractCookie(string setCookie, string key)
        {
            if (string.IsNullOrEmpty(setCookie) || string.IsNullOrEmpty(key)) return null;
            // key=VALUE; ...  에서 VALUE만 추출
            var rx = new Regex($@"(?:^|;)\s*{Regex.Escape(key)}=([^;]+)", RegexOptions.IgnoreCase);
            var m = rx.Match(setCookie);
            if (!m.Success) return null;
            return UnityWebRequest.UnEscapeURL(m.Groups[1].Value);
        }

        public async Task<(bool ok, string message)> LogoutAsync()
        {
            var url = $"{_base}/api/auth/logout";

            var (status, text, result, error) = await HttpClientBase.PostJsonAuto(url, json: null, auth: true);

            // 로컬 토큰은 무조건 정리 (네트워크 실패여도 클라 로그아웃)
            ClearToken();

            if (status is >= 200 and < 300)
            {
                try
                {
                    var ar = JsonUtility.FromJson<ApiResult>(text);
                    if (!string.IsNullOrWhiteSpace(ar?.message)) return (true, ar.message);
                }
                catch { }
                return (true, "로그아웃 완료");
            }

            if (status == 401) return (true, "이미 로그아웃 상태입니다(401).");
            try
            {
                var ar = JsonUtility.FromJson<ApiResult>(text);
                if (!string.IsNullOrWhiteSpace(ar?.message)) return (false, ar.message);
            }
            catch { }
            return (false, $"로그아웃 실패 (HTTP {status})");
        }

        public async Task<(bool ok, string accessToken, string message)> RefreshAsync()
        {
            var savedRefreshToken = PlayerPrefs.GetString(KEY_REFRESH, null);
            if (string.IsNullOrEmpty(savedRefreshToken))
            {
                ClearToken();
                return (false, null, "리프레시 토큰이 없습니다(클라이언트).");
            }

            var url = $"{_base}/api/auth/refresh";
            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Accept", "application/json");

            // 서버가 Set-Cookie로 줬던 키 이름("refreshToken")과 동일하게 설정
            req.SetRequestHeader("Cookie", $"refreshToken={savedRefreshToken}");

            // (만약 서버가 헤더 방식을 원한다면 아래 주석을 사용)
            // req.SetRequestHeader("Authorization", $"Bearer {savedRefreshToken}");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            var status = (int)req.responseCode;
            var text = req.downloadHandler?.text?.Trim() ?? "";
            var ctype = req.GetResponseHeader("Content-Type") ?? "";

            // 네트워크/서버 에러 처리
            if (req.result != UnityWebRequest.Result.Success)
            {
                // 401이면 리프레시 토큰도 만료된 것 -> 로그아웃 처리
                if (status == 401)
                {
                    ClearToken();
                    return (false, null, "리프레시 토큰이 만료되었습니다. 다시 로그인해주세요.");
                }
                return (false, null, $"HTTP {(long)req.responseCode}: {req.error}");
            }

            if (status is >= 200 and < 300)
            {
                string newAccess = null, newRefresh = null;

                // 1) 바디 JSON에서 추출
                if (text.Length > 0 && (ctype.Contains("json") || text.StartsWith("{")))
                {
                    try
                    {
                        var tok = JsonUtility.FromJson<TokenResponseDto>(text);
                        if (tok != null)
                        {
                            if (!string.IsNullOrEmpty(tok.accessToken)) newAccess = tok.accessToken;
                            if (!string.IsNullOrEmpty(tok.refreshToken)) newRefresh = tok.refreshToken;
                        }
                    }
                    catch { /* ignore */ }

                    if (string.IsNullOrEmpty(newAccess))
                        newAccess = ExtractStringValue(text, "accessToken", "access_token", "token", "access");
                    if (string.IsNullOrEmpty(newRefresh))
                        newRefresh = ExtractStringValue(text, "refreshToken", "refresh_token", "refresh");
                }

                // 2) 쿠키(Set-Cookie)에서 추출(서버가 쿠키만 갱신하는 케이스)
                var setCookie = req.GetResponseHeader("Set-Cookie") ?? "";
                if (string.IsNullOrEmpty(newAccess)) newAccess = ExtractCookie(setCookie, "accessToken");
                if (string.IsNullOrEmpty(newRefresh)) newRefresh = ExtractCookie(setCookie, "refreshToken");

                if (!string.IsNullOrEmpty(newAccess))
                {
                    PlayerPrefs.SetString(KEY_ACCESS, newAccess);
                    // 리프레시 토큰도 새로 왔다면 갱신 (Rotation 적용 시)
                    if (!string.IsNullOrEmpty(newRefresh))
                    {
                        PlayerPrefs.SetString(KEY_REFRESH, newRefresh);
                    }
                    PlayerPrefs.Save();

                    Debug.Log("[Auth] 토큰 재발급 성공");
                    return (true, newAccess, "토큰 재발급 완료");
                }

                return (false, null, "토큰 재발급 응답을 해석할 수 없습니다.");
            }

            // 400/401/404 → 리프레시 토큰 만료/무효. 로컬 토큰 정리 권장.
            ClearToken();
            return (false, null, $"토큰 재발급 실패 (HTTP {status})");
        }

        static string HashPw(string s)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var b = sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
            var sb = new StringBuilder();
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        public async Task<(bool ok, string message)> ChangePasswordAsync(string oldPassword, string newPassword)
        {
            var url = $"{_base}/api/auth/change-password";

            // 익명객체 대신 직렬화 가능한 DTO 사용
            var body = JsonUtility.ToJson(new ChangePwReq
            {
                oldPassword = oldPassword,
                newPassword = newPassword
            });

            var (status, text, result, error) = await HttpClientBase.PatchJsonAuto(url, body, auth: true);

            // 200~299 또는 204(No Content)까지 성공으로 처리
            if (result == UnityWebRequest.Result.Success && status >= 200 && status < 300)
            {
                PlayerPrefs.SetString("auth.pwHash", HashPw(newPassword));
                PlayerPrefs.Save();
                return (true, "비밀번호가 변경되었습니다.");
            }

            return (false, $"변경 실패 (HTTP {status})");
        }

        private string GetFriendlyErrorMessage(long statusCode)
        {
            switch (statusCode)
            {
                case 400: return "요청 형식이 올바르지 않습니다.";
                case 401: return "아이디 또는 비밀번호가 일치하지 않습니다."; // 로그인 실패
                case 403: return "접근 권한이 없습니다.";
                case 404: return "서버 주소를 찾을 수 없습니다.";
                case 409: return "이미 존재하는 계정입니다.";
                case 500: return "서버 내부 오류가 발생했습니다. 잠시 후 다시 시도해주세요.";
                case 502: return "서버 게이트웨이 오류입니다.";
                case 503: return "서버가 점검 중이거나 과부하 상태입니다.";
                case 0: return "서버와 연결할 수 없습니다. 네트워크를 확인해주세요.";
                default: return $"알 수 없는 오류가 발생했습니다. (코드: {statusCode})";
            }
        }
    }
}
