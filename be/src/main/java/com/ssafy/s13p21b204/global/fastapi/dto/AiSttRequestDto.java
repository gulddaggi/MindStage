package com.ssafy.s13p21b204.global.fastapi.dto;

import com.fasterxml.jackson.annotation.JsonProperty;

public record AiSttRequestDto(
    @JsonProperty("stt_url")
    String sttUrl
) {

}
