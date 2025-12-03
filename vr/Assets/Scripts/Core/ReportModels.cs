using System;
using System.Collections.Generic;

namespace App.Services
{
    [Serializable]
    public class ReportListItem
    {
        public string id;
        public string companyName;
        public string jobName;
        public string createdAt; // ISO 문자열 그대로 사용(화면 포맷팅은 컨트롤러에서)
    }

    [Serializable]
    public struct HeartRateSeries
    {
        public float[] values;
    }

    [Serializable]
    public class QuestionReport
    {
        public string question;        // 일반 질문
        public string relatedQuestion; // 꼬리 질문
        public string answer;
        public int[] labels;
    }

    [Serializable]
    public class ReportDetail
    {
        public string analysisSummary;     // = comment
        public float[] myScores;           // 5개(0~100)
        public float[] peerAvgScores;      // 없으면 null
        public HeartRateSeries heart;
        public List<QuestionReport> questions;
    }
}
