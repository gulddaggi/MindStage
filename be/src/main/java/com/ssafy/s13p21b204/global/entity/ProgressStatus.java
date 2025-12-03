package com.ssafy.s13p21b204.global.entity;

public enum ProgressStatus {
  CREATING, // 자소서 등록 이후 VR 면접 질문 생성 중
  NOT_STARTED, // VR 면접 질문 생성 완료 면접 시작 가능
  IN_PROGRESS, // 면접 진행 중
  COMPLETED, // 면접 종료 레포트 생성 중
  REPORTED, // 레포트 생성 완료
  FAILED // 면접 질문 생성 실패
}