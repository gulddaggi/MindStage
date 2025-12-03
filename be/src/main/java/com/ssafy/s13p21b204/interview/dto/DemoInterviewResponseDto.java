package com.ssafy.s13p21b204.interview.dto;

import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "시연용 면접 생성 응답 DTO")
public record DemoInterviewResponseDto(
    @Schema(description = "면접 ID", example = "123")
    Long interviewId,
    
    @Schema(description = "질문 ID", example = "456")
    Long questionId,
    
    @Schema(description = "질문 오디오 파일 Presigned URL", example = "https://s3.amazonaws.com/bucket/interviews/demo/intro_question.wav?X-Amz-Algorithm=...")
    String questionPresignedUrl,
    
    @Schema(
        description = "1분 자기소개용 더미데이터 (프론트에서 보여주기용)", 
        example = "안녕하세요. 저는 OO대학교 컴퓨터공학과를 졸업한 홍길동입니다. ..."
    )
    String exampleIntroduction
) {
}

