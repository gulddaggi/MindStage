using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System;
using App.Infra;
using UnityEngine;
using UnityEngine.Networking;


namespace App.Services
{
    /// <summary>/api/s3/presigned-url 호출 + S3로 업/다운로드.</summary>

    public class S3ApiService : IS3Service
    {
        [Serializable] class PresignReq { public string key; public string method; public string contentType; }
        [Serializable] class PresignRes { public string url; public string method; public Header[] headers; }
        [Serializable] class Header { public string name; public string value; }

        [Serializable]
        class PresignReqDto            // 업로드용: dir+fileName
        {
            public string directory;     // "interview/<sessionId>"
            public string fileName;      // "q01.wav"
            public string fileType;      // "UPLOAD"
            public string contentType;   // "audio/wav"
        }

        [Serializable]
        class PresignReqByKeyDto
        {
            public string fileKey;     // 서버가 준 저장 키
            public string fileType;    // "download"  (소문자)
            public string fileName;    // ★ 서버가 요구
        }

        [Serializable]
        class PresignResDto            // 서버 응답
        {
            public string url;           // presigned URL
            public string presignedUrl;  // 위 대신 이 이름일 수도 있음
            public string fileKey;       // ★ 서버가 생성한 최종 저장 키
            public string fileName;      // ★ (선택) 파일명
            public Header[] headers;
        }

        [Serializable]
        class PresignDownloadReqDto
        {
            public string directory;   // 예: "interview/<sessionId>"
            public string fileName;    // 예: "55f6d133-..._q01.wav"
            public string fileType;    // "download" (소문자)
        }

        class PresignDownloadReq // 서버가 어떤 필드를 기대하든 모두 보냄
        {
            public string fileKey;    // 최우선: 업로드 응답에서 받은 최종 키
            public string directory;  // 예: interview/<sessionId>
            public string fileName;   // 예: <UUID>_q01.wav  ← 반드시 UUID 포함된 실제 이름
            public string fileType;   // "download" (소문자)
        }

        Dictionary<string, string> ToDict(Header[] hs)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (hs != null) foreach (var h in hs) if (!string.IsNullOrEmpty(h?.name)) d[h.name] = h.value ?? "";
            return d;
        }

        static void SplitKey(string key, out string dir, out string name)
        {
            int i = key.LastIndexOf('/');
            if (i >= 0) { dir = key.Substring(0, i); name = key.Substring(i + 1); }
            else { dir = null; name = key; }
        }

        public async Task<PresignedUrl> GetPresignedUrlAsync(string method, string key, string contentType = null)
        {
            // key → directory / fileName 분리
            int slash = key.LastIndexOf('/');
            var directory = (slash > 0) ? key.Substring(0, slash) : null;
            var fileName = (slash >= 0) ? key.Substring(slash + 1) : key;

            var req = new PresignReqDto
            {
                directory = directory,
                fileName = fileName,
                fileType = "upload",
                contentType = contentType ?? "audio/wav"
            };

            var res = await HttpClientBase.PostJson<PresignResDto>("/api/s3/presigned-url", req);
            var finalUrl = !string.IsNullOrEmpty(res.url) ? res.url : res.presignedUrl;

            return new PresignedUrl
            {
                url = finalUrl,
                method = "PUT",
                headers = ToDict(res.headers),
                fileKey = res.fileKey,           // 업로드 후 저장
                fileName = string.IsNullOrEmpty(res.fileName) ? fileName : res.fileName
            };
        }

        public async Task UploadWithPresignedUrlAsync(byte[] data, PresignedUrl pre)
        {
            using var uwr = new UnityWebRequest(pre.url, UnityWebRequest.kHttpVerbPUT)
            {
                uploadHandler = new UploadHandlerRaw(data),
                downloadHandler = new DownloadHandlerBuffer()
            };
            if (pre.headers == null || !pre.headers.ContainsKey("Content-Type"))
                uwr.SetRequestHeader("Content-Type", "audio/wav");
            if (pre.headers != null)
                foreach (var kv in pre.headers) uwr.SetRequestHeader(kv.Key, kv.Value ?? "");

            var op = uwr.SendWebRequest();
            while (!op.isDone) await System.Threading.Tasks.Task.Yield();
            if (uwr.result != UnityWebRequest.Result.Success)
                throw new Exception($"S3 PUT failed: {uwr.responseCode} {uwr.error}");
        }

        public async Task<byte[]> DownloadWithPresignedUrlAsync(PresignedUrl pre)
        {
            using var uwr = UnityWebRequest.Get(pre.url);
            if (pre.headers != null)
                foreach (var kv in pre.headers) uwr.SetRequestHeader(kv.Key, kv.Value ?? "");

            var op = uwr.SendWebRequest();
            while (!op.isDone) await System.Threading.Tasks.Task.Yield();
            if (uwr.result != UnityWebRequest.Result.Success)
                throw new Exception($"S3 GET failed: {uwr.responseCode} {uwr.error}\n{uwr.downloadHandler?.text}");
            return uwr.downloadHandler.data;
        }

        public async Task<PresignedUrl> GetPresignedUrlByFileKeyAsync(string fileKey, string fileName = null)
        {
            // fileKey에서 directory, name 분리
            SplitKey(fileKey, out var dir, out var name);
            var req = new PresignDownloadReq
            {
                fileKey = fileKey,                  // 서버가 이걸 쓰면 그대로 매칭
                directory = dir,                      // 서버가 dir+filename 조합을 쓰면 이걸로 매칭
                fileName = string.IsNullOrEmpty(fileName) ? name : fileName,
                fileType = "download"
            };

            Debug.Log($"[S3][DN-REQ] {JsonUtility.ToJson(req)}");

            var res = await HttpClientBase.PostJson<PresignResDto>("/api/s3/presigned-url", req);
            var finalUrl = !string.IsNullOrEmpty(res.url) ? res.url : res.presignedUrl;

            // 서버가 어떤 키로 서명했는지 확인
            var keyInUrl = InferKeyFromPresignedUrl(finalUrl);
            if (!string.Equals(keyInUrl, fileKey, StringComparison.Ordinal))
                Debug.LogWarning($"[S3][DN-MISMATCH] want={fileKey}  signed={keyInUrl}");

            return new PresignedUrl
            {
                url = finalUrl,
                method = "GET",
                headers = ToDict(res.headers),
                fileKey = fileKey,
                fileName = string.IsNullOrEmpty(res.fileName) ? req.fileName : res.fileName
            };
        }


        static string InferKeyFromPresignedUrl(string url)
        {
            // 예) https://bucket.s3.ap-northeast-2.amazonaws.com/interview/SESSION/q01.wav?... 
            //     → "/interview/SESSION/q01.wav" 에서 앞의 '/' 제거
            try
            {
                var u = new Uri(url);
                var path = u.AbsolutePath;                 // "/interview/SESSION/q01.wav"
                if (string.IsNullOrEmpty(path)) return null;
                // path-style일 경우 "/bucket/key" 형태일 수 있음 → 버킷명 제거 시도
                // 하지만 대부분 virtual-hosted-style이므로 우선 그대로 사용
                return path.TrimStart('/');
            }
            catch { return null; }
        }

    }
}
