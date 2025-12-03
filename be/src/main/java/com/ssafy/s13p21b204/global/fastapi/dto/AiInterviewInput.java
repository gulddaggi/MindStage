package com.ssafy.s13p21b204.global.fastapi.dto;

import com.fasterxml.jackson.annotation.JsonProperty;
import java.util.List;
import java.util.Map;

/**
 * FastAPI 인터뷰 API 요청 DTO
 * 모든 필드를 포함하도록 null 값은 빈 문자열/빈 배열로 변환하여 전송
 */
public record AiInterviewInput(
    @JsonProperty("jd_presigned_url")
    String jd,
    @JsonProperty("resume")
    List<AiResumeQAItem> resume,

    @JsonProperty("qna_history")
    List<Map<String, String>> qnaHistory,

    @JsonProperty("latest_wav_file_url")
    String latestWavFileUrl,

    @JsonProperty("saved_tts_file_url")
    List<String> savedTtsFileUrl
) {}
