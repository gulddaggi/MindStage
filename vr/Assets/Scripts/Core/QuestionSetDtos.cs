/// <summary>질문 세트 요약/상세와 시퀀스 카운터를 정의.</summary>
namespace App.Core
{
    [System.Serializable]
    public class QuestionSetSummary
    {
        public string id;              // 예) C01_J02_S003 (회사-직무별 생성순)
        public string companyName;     // 표시용 회사명
        public string jobName;         // 표시용 직무명
        public string createdAtIso;    // 생성 시간(ISO 8601)
    }

    [System.Serializable]
    public class QuestionSetDetail
    {
        public string id;
        public string companyName;
        public string jobName;
        public string createdAtIso;
        public InterviewQuestion[] items; // 면접 진행용 질문 배열(더미라도 유지)
    }

    [System.Serializable]
    public class QSetSeqCounter
    {
        public string companyId;
        public string jobId;
        public int nextSeq; // 해당 조합의 다음 시퀀스
    }

    // PlayerPrefs 저장 컨테이너
    [System.Serializable]
    public class QSetSave
    {
        public QuestionSetDetail[] records;
        public QSetSeqCounter[] counters;
    }
}
