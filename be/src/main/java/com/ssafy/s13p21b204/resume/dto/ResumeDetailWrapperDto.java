package com.ssafy.s13p21b204.resume.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import java.time.LocalDateTime;
import java.util.List;

@Schema(description = "자소서 상세 응답 DTO")
public record ResumeDetailWrapperDto(
    @Schema(description = "답변 목록")
    List<ResumeDetailResponseDto> answers,
    
    @Schema(description = "자소서 작성일시", example = "2025-11-04T15:30:00")
    LocalDateTime resumeCreatedAt
) {
}

