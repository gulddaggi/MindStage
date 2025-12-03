package com.ssafy.s13p21b204.interview.dto;

import com.ssafy.s13p21b204.interview.entity.InterviewQuestion.Difficult;
import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "꼬리질문 응답 DTO")
public record RelatedQuestionResponseDto(
    @Schema(description = "꼬리질문 ID", example = "6")
    Long questionId,
    
    @Schema(description = "꼬리질문 오디오 파일 다운로드용 Presigned URL", example = "https://s3.amazonaws.com/bucket/related-question1.wav?X-Amz-Algorithm=...")
    String preSignedUrl,
    
    @Schema(description = "꼬리질문 내용", example = "그 경험을 통해 무엇을 배우셨나요?")
    String content,
    @Schema(description = "질문의 난이도",example = "STRICT")
    Difficult difficult
) {

}
