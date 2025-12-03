using System;
using System.Collections.Generic;

namespace App.Services
{
    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public string message;
        public int code;
        public T data;
    }

    [Serializable]
    public class ReportListRowDto
    {
        public string reportId;
        public string CompanyName; // 서버 대문자 키 유지
        public string part;
        public string createdAt;
    }

    [Serializable]
    public class ScoresDto
    {
        public int Job_Competency;
        public int Teamwork_Leadership;
        public int Communication;
        public int Integrity;
        public int Adaptability;
    }

    [Serializable]
    public class QnaItemDto
    {
        public string question;         // 일반 질문
        public string relatedQuestion;  // 꼬리 질문
        public string answer;
        public int[] labels;
    }

    [Serializable]
    public class ReportDetailDto
    {
        public string comment;
        public List<HeartBeatSampleDto> heartBeats;
        public ScoresDto myScores;
        public ScoresDto averageScores;
        public List<QnaItemDto> qnaList;
    }

    [Serializable]
    public class HeartBeatSampleDto
    {
        public int bpm;
        public string measureAt;
    }
}
