package com.ssafy.s13p21b204.answer.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.Size;

@Schema(
    description = "자소서 답변 요청 DTO",
    example = """
        {
          "questionId": 1,
          "content": "저는 현대오토에버의 소프트웨어 개발 직무에 지원하게 되었습니다. 대학 시절 웹 개발 프로젝트를 진행하면서..."
        }
        """
)
public record AnswerRequestDto(
    @Schema(
        description = "질문 ID",
        example = "1",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotNull(message = "질문 ID는 필수입니다.")
    @Positive(message = "질문 ID는 양수여야 합니다.")
    Long questionId,

    @Schema(
        description = "답변 내용 (최소 10자 이상, 최대 글자수는 각 질문의 제한에 따름)",
        example = "저는 현대오토에버의 소프트웨어 개발 직무에 지원하게 되었습니다...",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @Size(min = 10, message = "답변은 최소 10자 이상 작성해주세요.")
    String content
) {

}
