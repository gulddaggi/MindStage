package com.ssafy.s13p21b204.interview.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;

@Schema(description = "인터뷰 답변 등록 요청 DTO")
public record InterviewReplyRequestDto(
    @Schema(description = "인터뷰 질문 ID", example = "1", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "질문 ID는 필수입니다.")
    @Positive(message = "질문 ID는 양수여야 합니다.")
    Long questionId,
    
    @Schema(description = "S3에 업로드된 오디오 파일의 키", example = "interviews/audio/uuid_interview-audio.wav", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotBlank(message = "S3 키는 필수입니다.")
    String s3Key
) {

}
