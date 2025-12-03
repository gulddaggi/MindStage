package com.ssafy.s13p21b204.resume.dto;

import com.ssafy.s13p21b204.global.entity.ProgressStatus;
import com.ssafy.s13p21b204.jobPosting.entity.JobPosting.Part;
import com.ssafy.s13p21b204.resume.entity.Resume;
import io.swagger.v3.oas.annotations.media.Schema;
import java.time.LocalDateTime;

@Schema(description = "자소서 응답 DTO")
public record ResumeResponseDto(
    @Schema(description = "자소서 ID", example = "1")
    Long resumeId,

    @Schema(description = "회사명", example = "삼성전자")
    String companyName,

    @Schema(description = "지원 직무", example = "SOFTWARE")
    Part part,

    @Schema(description = "자소서 작성일", example = "2025-11-04T15:30:00")
    LocalDateTime createdAt,

    @Schema(description = "인터뷰 ID (인터뷰가 생성되지 않은 경우 null)", example = "1")
    Long interviewId,

    @Schema(description = "인터뷰 준비 상태")
    ProgressStatus progressStatus
) {
  /**
   * Resume 엔티티를 DTO로 변환
   */
  public static ResumeResponseDto from(Resume resume) {
    return new ResumeResponseDto(
        resume.getResumeId(),
        resume.getJobPosting().getCompany().getName(),
        resume.getJobPosting().getPart(),
        resume.getCreatedAt(),
        null,  // Interview ID랑 상태는 별도로 전달 필요
        null
    );
  }

  /**
   * Resume과 Interview ID를 함께 받아 DTO로 변환
   */
  public static ResumeResponseDto from(Resume resume, Long interviewId, ProgressStatus progressStatus) {
    return new ResumeResponseDto(
        resume.getResumeId(),
        resume.getJobPosting().getCompany().getName(),
        resume.getJobPosting().getPart(),
        resume.getCreatedAt(),
        interviewId,
        progressStatus
    );
  }

}
