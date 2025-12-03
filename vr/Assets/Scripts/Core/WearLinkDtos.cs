namespace App.Core
{
    public enum WearLinkState { Disconnected, Pending, Linked }

    [System.Serializable]
    public class WearLinkStatus
    {
        public WearLinkState state;     // Disconnected | Linked (서버는 Pending 개념 없음)
        public int? galaxyWatchId;      // 등록된 워치 PK (없으면 null)
        public string modelName;        // 예: "Galaxy Watch 6"
        public string uuid;             // 등록 요청 시 사용(조회 응답에는 안 올 수 있음)
        public int ttlSeconds;          // 서버 스펙엔 없음. UI 호환용 0 고정 사용
    }

    // POST /api/GalaxyWatch/register 바디
    [System.Serializable]
    public class WearLinkRegisterRequest
    {
        public string uuid;
        public string modelName;
    }

    // 서버 data 페이로드 형태(조회·등록 공통)
    [System.Serializable]
    public class GalaxyWatchInfo
    {
        public int galaxyWatchId;
        public string modelName;
    }
}