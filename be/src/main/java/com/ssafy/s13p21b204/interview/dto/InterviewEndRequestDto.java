package com.ssafy.s13p21b204.interview.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;

@Schema(description = "면접 종료 요청 DTO")
public record InterviewEndRequestDto(
    @Schema(description = "인터뷰 ID", example = "1", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "인터뷰 ID는 필수입니다.")
    @Positive(message = "인터뷰 ID는 양수여야 합니다.")
    Long interviewId,
    
    @Schema(
        description = "면접 전체 녹화 음성 파일 S3 키", 
        example = "interviews/audio/interview_1_audio.wav", 
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotBlank(message = "면접 음성 파일 S3 키는 필수입니다.")
    String s3Key
) {
}

