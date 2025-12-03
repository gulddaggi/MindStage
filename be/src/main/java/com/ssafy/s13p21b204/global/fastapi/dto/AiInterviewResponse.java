package com.ssafy.s13p21b204.global.fastapi.dto;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;
import io.swagger.v3.oas.annotations.media.Schema;

import java.util.List;

/**
 * FastAPI 면접 시작/답변 API 응답
 */
@Schema(description = "면접 시작/답변 API 응답")
@JsonIgnoreProperties(ignoreUnknown = true)
public record AiInterviewResponse(
    @Schema(description = "상태 (success, error)", example = "success")
    @JsonProperty("status")
    String status,

    @Schema(description = "STT로 변환된 텍스트 (첫 시작 시에는 null)", example = "저는 3년간 백엔드 개발...")
    @JsonProperty("converted_text_with_stt")
    String convertedTextWithStt,

    @Schema(description = "TTS로 전환할 질문 텍스트 리스트", example = "[\"귀하의 강점에 대해 말씀해주세요.\"]")
    @JsonProperty("text_from_tts")
    List<String> textFromTts,

    @Schema(description = "면접관 타입 (0: 엄격한 면접관, 1: 친근한 면접관)", example = "[0, 1]")
    @JsonProperty("talker")
    List<Integer> talker
) {}


