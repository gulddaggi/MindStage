using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>자기소개서/회사/직무/문항 DTO(백엔드 스키마에 맞춰 조정 예정).</summary>

namespace App.Core
{
    [System.Serializable] 
    public class Company
    { 
        public string id; 
        public string name; 
    }

    [System.Serializable] 
    public class JobRole 
    { 
        public string id; 
        public string name; 
        public string companyId; 
    }

    [System.Serializable] 
    public class Question 
    { 
        public string id; 
        public string number; 
        public string text; 
    }

    [System.Serializable]
    public class ResumeListItem
    {
        public string id; 
        public string title;
        public string companyId; 
        public string companyName;
        public string jobId; 
        public string jobName;
        public string modifiedAt;   // ISO string
        public bool hasQuestionSet; // 세트 상태 배지
        public string interviewId;
        public string progressStatus; // CREATING/NOT_STARTED/IN_PROGRESS/COMPLETED/REPORTED/FAILED
    }

    [System.Serializable]
    public class ResumeDetail
    {
        public string id;
        public string companyId; public string jobId;
        public Question[] questions;          // 선택한 직무의 문항 목록
        public System.Collections.Generic.List<AnswerItem> answers = new();
    }

    [System.Serializable] 
    public class AnswerItem 
    { 
        public string questionId; 
        public string answer; 
    }

    [System.Serializable]
    public class ResumeAnswerViewDto
    {
        public string question;   // 서버가 내려주는 질문 본문
        public string answer;     // 저장된 답변 본문
    }

    [System.Serializable]
    public class ResumeDetailDto
    {
        public ResumeAnswerViewDto[] answers;
        public string resumeCreatedAt; // "2025-11-04T15:30:00"
    }

    // 공통 래퍼(이미 있다면 재사용)
    [System.Serializable]
    public class ApiEnvelope<T>
    {
        public bool success;
        public string message;
        public int code;
        public T data;
    }
}