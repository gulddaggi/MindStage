using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace App.Services
{
    /// <summary>S3 presigned URL 발급/업로드/다운로드를 캡슐화.</summary>

    public interface IS3Service
    {
        Task<PresignedUrl> GetPresignedUrlAsync(string method, string key, string contentType = null);
        Task UploadWithPresignedUrlAsync(byte[] data, PresignedUrl pre);     // PUT
        Task<byte[]> DownloadWithPresignedUrlAsync(PresignedUrl pre);        // GET

        Task<PresignedUrl> GetPresignedUrlByFileKeyAsync(string fileKey, string fileName = null);
    }

    /// <summary>서버에서 받은 presigned URL + 헤더들.</summary>
    public class PresignedUrl
    {
        public string url;
        public string method;
        public Dictionary<string, string> headers;
        public string fileKey;   // ★ 서버가 돌려준 S3 저장 키
        public string fileName;  // ★ (있으면 사용) 다운로드 파일명 힌트
    }
}
