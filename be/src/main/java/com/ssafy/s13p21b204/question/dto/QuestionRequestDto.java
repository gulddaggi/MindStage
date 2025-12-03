package com.ssafy.s13p21b204.question.dto;

import com.fasterxml.jackson.annotation.JsonProperty;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

@Schema(description = "면접 질문 요청 DTO")
public record QuestionRequestDto(
    @JsonProperty("question")
    @Schema(
        description = "면접 질문 내용", 
        example = "현대오토에버의 해당 직무에 지원한 이유와 앞으로 현대오토에버에서 키워 나갈 커리어 계획을 작성해주시기 바랍니다.",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotBlank(message = "질문 내용은 필수입니다.")
    @Size(min = 1, max = 500, message = "질문은 1자 이상 500자 이하로 입력해주세요.")
    String question,
    
    @JsonProperty("limit")
    @Schema(
        description = "답변 글자 수 제한 (최대 입력 가능한 글자 수)", 
        example = "1000",
        minimum = "100",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotNull(message = "답변 글자 수 제한은 필수입니다.")
    @Min(value = 10, message = "답변 글자 수 제한은 최소 10자 이상이어야 합니다.")
    Integer limit
) {

}
