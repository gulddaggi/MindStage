package com.ssafy.s13p21b204.resume.dto;

import com.ssafy.s13p21b204.answer.entity.Answer;
import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "자소서 상세 답변 응답 DTO")
public record ResumeDetailResponseDto(
    @Schema(description = "질문 내용", example = "지원 동기를 작성해주세요.")
    String question,
    
    @Schema(description = "답변 내용", example = "저는 귀사의 비전에 공감하여...")
    String answer
) {
    /**
     * Answer 엔티티를 DTO로 변환
     * 데이터 변환만 수행하며, 검증은 서비스 레이어에서 수행
     * 
     * @param answer 답변 엔티티 (Question이 로드되어 있어야 함)
     * @return ResumeDetailResponseDto
     */
    public static ResumeDetailResponseDto from(Answer answer) {
        return new ResumeDetailResponseDto(
            answer.getQuestion().getContent(),  // 질문 내용
            answer.getContent() != null ? answer.getContent() : ""  // 답변 내용 (null 방지)
        );
    }
}
