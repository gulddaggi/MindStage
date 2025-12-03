package com.ssafy.s13p21b204.interview.dto;

import com.ssafy.s13p21b204.global.entity.ProgressStatus;
import com.ssafy.s13p21b204.interview.entity.Interview;
import io.swagger.v3.oas.annotations.media.Schema;
import java.time.LocalDateTime;

@Schema(description = "인터뷰 응답 DTO")
public record InterviewResponseDto(
    @Schema(description = "인터뷰 ID", example = "1")
    Long interviewId,
    
    @Schema(description = "자소서 ID", example = "1")
    Long resumeId,
    
    @Schema(description = "꼬리질문 사용 여부", example = "true")
    Boolean relatedQuestion,
    
    @Schema(description = "인터뷰 진행 상태", example = "IN_PROGRESS")
    ProgressStatus status,
    
    @Schema(description = "인터뷰 생성일시", example = "2025-01-15T10:30:00")
    LocalDateTime createdAt
) {

    public static InterviewResponseDto of(Interview interview) {
        return new InterviewResponseDto(
            interview.getInterviewId(),
            interview.getResume() != null ? interview.getResume().getResumeId() : null,
            interview.getRelatedQuestion(),
            interview.getProgressStatus(),
            interview.getCreatedAt()
        );
    }
}
