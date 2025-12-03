namespace App.Core
{
    [System.Serializable]
    public class InterviewQuestion { public string id; public string text; public string[] tags; }

    [System.Serializable]
    public class InterviewSet { public string id; public string title; public InterviewQuestion[] items; }

    [System.Serializable]
    public class InterviewSession
    {
        public string sessionId;
        public string setId;
        public int index;               // 현재 질문 인덱스
        public float prepSeconds = 3f;  // 준비 타이머(예: 3초)
        public float answerSeconds = 90f;
    }
}
