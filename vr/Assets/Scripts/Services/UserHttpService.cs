using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Infra;
using UnityEngine;
using UnityEngine.Networking;

namespace App.Services
{
    /// <summary>/api/user/me (JWT 필요). HttpClientBase.GetAuto로 401 시 자동 재발급+재시도.</summary>
    public class UserHttpService : IUserService
    {
        const string PATH = "/api/user/me";
        readonly string _base;

        public UserHttpService(string baseUrl) => _base = baseUrl?.TrimEnd('/') ?? "";
        public UserHttpService() : this(HttpClientBase.BaseUrl) { }

        [System.Serializable] class DataWrap { public bool success; public string message; public int code; public UserDto data; }
        [System.Serializable] class UserWrap { public bool success; public string message; public int code; public UserDto user; }

        public async Task<(bool ok, UserDto user, string message)> GetMeAsync()
        {
            var url = _base + PATH;
            var res = await HttpClientBase.GetAuto(url, auth: true);

            if (res.result == UnityWebRequest.Result.Success && res.status >= 200 && res.status < 300)
            {
                try
                {
                    // 1) 순수 UserDto
                    var direct = JsonUtility.FromJson<UserDto>(res.text);
                    if (IsValid(direct)) return (true, direct, null);

                    // 2) { data: UserDto, ... }
                    var w1 = JsonUtility.FromJson<DataWrap>(res.text);
                    if (IsValid(w1?.data)) return (true, w1.data, null);

                    // 3) { user: UserDto, ... }
                    var w2 = JsonUtility.FromJson<UserWrap>(res.text);
                    if (IsValid(w2?.user)) return (true, w2.user, null);

#if UNITY_EDITOR
                    Debug.LogWarning($"[UserHttpService] 예상치 못한 응답 형식: {res.text}");
#endif
                    return (false, null, "서버 응답 형식을 해석할 수 없습니다.");
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                    return (false, null, "서버 응답 형식을 해석할 수 없습니다.");
                }
            }

            return (false, null, $"HTTP {res.status} {res.error ?? res.text}");
        }

        static bool IsValid(UserDto u) => u != null && !string.IsNullOrEmpty(u.email);
    }
}
