using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Core;
using App.Infra;
using UnityEngine;
using UnityEngine.Networking;

namespace App.Services
{
    public class WearLinkHttpService : IWearLinkService
    {
        private readonly string _base; // ex) https://mindstage.duckdns.org

        [Serializable]
        private class ApiEnvelope<T>
        {
            public bool success;
            public string message;
            public long code;
            public T data;
        }

        public WearLinkHttpService(string baseUrl) => _base = baseUrl.TrimEnd('/');

        public async Task<WearLinkStatus> GetStatusAsync()
        {
            var url = $"{_base}/api/GalaxyWatch/me";
            var (status, text, result, error) = await HttpClientBase.GetAuto(url, auth: true);

            if (result != UnityWebRequest.Result.Success)
                throw new Exception($"[GET /me] {status} {error ?? text}");

            var raw = (text ?? "").Trim();
            if (string.IsNullOrEmpty(raw) || raw == "null")
                return Disconnected();

            // 빠른 차단: "data": null 패턴이면 바로 미연결
            if (raw.IndexOf("\"data\": null", StringComparison.OrdinalIgnoreCase) >= 0)
                return Disconnected();

            ApiEnvelope<GalaxyWatchInfo> env = null;
            try { env = JsonUtility.FromJson<ApiEnvelope<GalaxyWatchInfo>>(raw); } catch { /* ignore */ }

            GalaxyWatchInfo info = null;

            if (env != null && (env.success || raw.Contains("\"data\"")))
            {
                // ★ 핵심: null 이거나 "빈 객체"면 미연결
                if (env.data == null || IsEmpty(env.data))
                    return Disconnected();
                info = env.data;
            }
            else
            {
                try { info = JsonUtility.FromJson<GalaxyWatchInfo>(raw); } catch { }
                if (IsEmpty(info)) info = null;
            }

            if (info == null) return Disconnected();

            return new WearLinkStatus
            {
                state = WearLinkState.Linked,
                galaxyWatchId = info.galaxyWatchId,
                modelName = info.modelName,
                ttlSeconds = 0
            };

            // ---- helpers ----
            bool IsEmpty(GalaxyWatchInfo i)
                => i == null
                || (i.galaxyWatchId <= 0
                    && string.IsNullOrEmpty(i.modelName));

            WearLinkStatus Disconnected() => new WearLinkStatus
            {
                state = WearLinkState.Disconnected,
                ttlSeconds = 0
            };
        }

        public async Task<WearLinkStatus> RegisterAsync(WearLinkRegisterRequest req)
        {
            var url = $"{_base}/api/GalaxyWatch/register";
            var json = JsonUtility.ToJson(req);
            var (status, text, result, error) = await HttpClientBase.PostJsonAuto(url, json, auth: true);

            if (result != UnityWebRequest.Result.Success)
                throw new Exception($"[POST /register] {status} {error ?? text}");

            var info = TryParse<GalaxyWatchInfo>(text);
            return new WearLinkStatus
            {
                state = WearLinkState.Linked,
                galaxyWatchId = info?.galaxyWatchId,
                modelName = info?.modelName ?? req.modelName,
                uuid = req.uuid,
                ttlSeconds = 0
            };
        }

        public async Task UnlinkAsync()
        {
            var url = $"{_base}/api/GalaxyWatch/me";
            var (status, text, result, error) = await HttpClientBase.DeleteAuto(url, auth: true);
            if (result != UnityWebRequest.Result.Success)
                throw new Exception($"[DELETE /me] {status} {error ?? text}");
        }

        // ── helpers ──
        private static T TryParse<T>(string json)
        {
            try
            {
                var env = JsonUtility.FromJson<ApiEnvelope<T>>(json);
                if (!EqualityComparer<T>.Default.Equals(env.data, default(T))) return env.data;
            }
            catch { /* ignore */ }

            try { return JsonUtility.FromJson<T>(json); }
            catch { return default; }
        }
    }
}
