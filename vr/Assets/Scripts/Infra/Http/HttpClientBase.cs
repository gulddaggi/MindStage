using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>UnityWebRequest 래퍼: JSON GET/POST/PUT/DELETE + PDF 업로드 + Bearer 주입.</summary>

namespace App.Infra
{
    public static class HttpClientBase
    {
        public static string BaseUrl = "https://mindstage.duckdns.org";

        // 토큰 읽는 방법을 주입하거나, PlayerPrefs 직접 사용해도 됨
        public static System.Func<string> TokenProvider =
            () => PlayerPrefs.GetString("auth.accessToken", null);

        static string GetToken()
        {
            // MVP: PlayerPrefs 기반(추후 안전 저장소 or Services.Auth에서 직접 제공)
            return PlayerPrefs.GetString("auth.accessToken", null);
        }

        static async Task<(int status, string text, UnityWebRequest.Result result, string error)>
        SendOnce(UnityWebRequest req, bool auth)
        {
            if (auth)
            {
                var at = TokenProvider?.Invoke();
                if (!string.IsNullOrEmpty(at))
                    req.SetRequestHeader("Authorization", $"Bearer {at}");
            }

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            var text = req.downloadHandler != null ? req.downloadHandler.text : "";
            return ((int)req.responseCode, text, req.result, req.error);
        }

        // GET (자동 재발급 + 1회 재시도)
        public static async Task<(int status, string text, UnityWebRequest.Result result, string error)>
            GetAuto(string url, bool auth = true)
        {
            async Task<(int status, string text, UnityWebRequest.Result result, string error)> Do()
            {
                using var req = UnityWebRequest.Get(url);
                req.downloadHandler = new DownloadHandlerBuffer();
                return await SendOnce(req, auth);
            }

            var res = await Do();
            if (auth && IsExpired(res.status, res.text))
            {
                var (ok, _, _) = await App.Infra.Services.Auth.RefreshAsync();
                if (ok) res = await Do(); // 1회 재시도
                else App.Infra.Services.Auth.ClearToken();
            }
            return res;
        }

        // POST JSON (자동 재발급 + 1회 재시도)
        public static async Task<(int status, string text, UnityWebRequest.Result result, string error)>
            PostJsonAuto(string url, string json, bool auth = true)
        {
            async Task<(int status, string text, UnityWebRequest.Result result, string error)> Do()
            {
                using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                req.downloadHandler = new DownloadHandlerBuffer();
                if (json != null)
                {
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                }
                return await SendOnce(req, auth);
            }

            var res = await Do();
            if (auth && IsExpired(res.status, res.text))
            {
                var (ok, _, _) = await App.Infra.Services.Auth.RefreshAsync();
                if (ok) res = await Do(); // 1회 재시도
                else App.Infra.Services.Auth.ClearToken();
            }
            return res;
        }

        public static async Task<(int status, string text, UnityWebRequest.Result result, string error)>
    DeleteAuto(string url, bool auth = true)
        {
            async Task<(int status, string text, UnityWebRequest.Result result, string error)> Do()
            {
                using var req = UnityWebRequest.Delete(url);
                req.downloadHandler = new DownloadHandlerBuffer(); // 본문을 쓰는 서버도 있으니 버퍼 달아둠
                return await SendOnce(req, auth);
            }

            var res = await Do();
            if (auth && IsExpired(res.status, res.text))
            {
                var (ok, _, _) = await App.Infra.Services.Auth.RefreshAsync();
                if (ok) res = await Do();
                else App.Infra.Services.Auth.ClearToken();
            }
            return res;
        }

        public static async Task<(int status, string text, UnityWebRequest.Result result, string error)>
    PatchJsonAuto(string url, string json, bool auth = true)
        {
            async Task<(int status, string text, UnityWebRequest.Result result, string error)> Do()
            {
                var req = new UnityWebRequest(url, "PATCH");
                if (json != null)
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                return await SendOnce(req, auth);
            }
            var res = await Do();
            if (auth && IsExpired(res.status, res.text))
            {
                var (ok, _, _) = await App.Infra.Services.Auth.RefreshAsync();
                if (ok) res = await Do(); else App.Infra.Services.Auth.ClearToken();
            }
            return res;
        }

        // 서버에서 만료를 알리는 패턴을 한곳에서 정의 (필요 시 보강)
        static bool IsExpired(int status, string body)
        {
            if (status == 401) return true;     // 표준
                                                // 일부 서버는 403/419, 또는 body에 code/message로 만료를 알리기도 함.
                                                // 간단 폴백:
            if (status == 403 || status == 419) return true;
            if (!string.IsNullOrEmpty(body) && body.IndexOf("expired", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        public static async Task<T> GetJson<T>(string path)
        {
            using var req = UnityWebRequest.Get(BaseUrl + path);
            var at = GetToken();

            if (!string.IsNullOrEmpty(at)) req.SetRequestHeader("Authorization", $"Bearer {at}");

            req.downloadHandler = new DownloadHandlerBuffer();
            var op = req.SendWebRequest(); while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new System.Exception($"HTTP {(long)req.responseCode} {req.error}\n{req.downloadHandler?.text}");

            return JsonUtility.FromJson<Wrapper<T>>(Wrap(req.downloadHandler.text)).data;
        }

        public static async Task<T> PostJson<T>(string path, object body)
        {
            var json = JsonUtility.ToJson(body);
            using var req = new UnityWebRequest(BaseUrl + path, "POST");
            var at = GetToken();

            if (!string.IsNullOrEmpty(at)) req.SetRequestHeader("Authorization", $"Bearer {at}");

            req.SetRequestHeader("Content-Type", "application/json");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            var op = req.SendWebRequest(); while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success) throw new System.Exception(
    $"HTTP {(long)req.responseCode} {req.error}\n{req.downloadHandler?.text}"
);

            return JsonUtility.FromJson<Wrapper<T>>(Wrap(req.downloadHandler.text)).data;
        }

        public static async Task<T> PutJson<T>(string path, object body)
        {
            var json = JsonUtility.ToJson(body);
            using var req = new UnityWebRequest(BaseUrl + path, "PUT");
            var at = GetToken();

            if (!string.IsNullOrEmpty(at)) req.SetRequestHeader("Authorization", $"Bearer {at}");

            req.SetRequestHeader("Content-Type", "application/json");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            var op = req.SendWebRequest(); while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success) throw new System.Exception(req.error);

            return JsonUtility.FromJson<Wrapper<T>>(Wrap(req.downloadHandler.text)).data;
        }

        public static async Task<bool> Delete(string path)
        {
            using var req = UnityWebRequest.Delete(BaseUrl + path);
            var at = GetToken();

            if (!string.IsNullOrEmpty(at)) req.SetRequestHeader("Authorization", $"Bearer {at}");
            var op = req.SendWebRequest();

            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success) throw new System.Exception(req.error);

            return true;
        }

        public static async Task<T> UploadPdf<T>(string path, byte[] bytes, string fileName = "jd.pdf")
        {
            var form = new WWWForm();
            form.AddBinaryData("file", bytes, fileName, "application/pdf");
            using var req = UnityWebRequest.Post(BaseUrl + path, form);
            var at = GetToken();

            if (!string.IsNullOrEmpty(at)) req.SetRequestHeader("Authorization", $"Bearer {at}");
            req.downloadHandler = new DownloadHandlerBuffer();
            var op = req.SendWebRequest();

            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success) throw new System.Exception(req.error);

            return JsonUtility.FromJson<Wrapper<T>>(Wrap(req.downloadHandler.text)).data;
        }

        // JsonUtility 한계 보완용 래퍼 (루트 배열 대응)
        [System.Serializable] class Wrapper<T> { public T data; }
        static string Wrap(string raw)
        {
            if (raw.TrimStart().StartsWith("{")) return raw; // 이미 객체
            return "{\"data\":" + raw + "}";
        }
    }
}

