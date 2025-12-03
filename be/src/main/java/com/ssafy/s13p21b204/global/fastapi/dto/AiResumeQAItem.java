package com.ssafy.s13p21b204.global.fastapi.dto;

import com.fasterxml.jackson.annotation.JsonProperty;
import io.swagger.v3.oas.annotations.media.Schema;

/**
 * FastAPI 면접 시작 API에서 사용하는 자소서 질문-답변 항목
 */
@Schema(description = "자소서 질문-답변 항목")
public record AiResumeQAItem(
    @Schema(description = "질문 번호", example = "1")
    @JsonProperty("num")
    String num,

    @Schema(description = "자소서 질문", example = "지원 동기를 작성해주세요.")
    @JsonProperty("question")
    String question,

    @Schema(description = "자소서 답변", example = "저는 귀사의 비전에 공감하여...")
    @JsonProperty("answer")
    String answer,

    @Schema(description = "답변 내용 (answer와 동일)", example = "저는 귀사의 비전에 공감하여...")
    @JsonProperty("content")
    String content
) {}


