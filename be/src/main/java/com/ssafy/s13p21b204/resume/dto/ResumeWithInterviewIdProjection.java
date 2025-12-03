package com.ssafy.s13p21b204.resume.dto;

import com.ssafy.s13p21b204.global.entity.ProgressStatus;
import com.ssafy.s13p21b204.resume.entity.Resume;

/**
 * Resume과 Interview ID, ProgressStatus를 함께 조회하는 DTO Projection
 * 타입 안전성을 보장하기 위해 인터페이스 기반 Projection 사용
 */
public interface ResumeWithInterviewIdProjection {
    Resume getResume();
    Long getInterviewId();
    ProgressStatus getProgressStatus();
}

