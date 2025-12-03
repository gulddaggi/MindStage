package com.ssafy.s13p21b204.interview.dto;

import com.ssafy.s13p21b204.interview.entity.InterviewQuestion.Difficult;
import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "인터뷰 질문 응답 DTO")
public record InterviewQuestionResponseDto(
    @Schema(description = "인터뷰 질문 ID", example = "1")
    Long interviewQuestionId,
    
    @Schema(description = "질문 오디오 파일 다운로드용 Presigned URL", example = "https://s3.amazonaws.com/bucket/question1.wav?X-Amz-Algorithm=...")
    String preSignedUrl,

    @Schema(description = "난이도", example = "LAX")
    Difficult difficult
) {

}
