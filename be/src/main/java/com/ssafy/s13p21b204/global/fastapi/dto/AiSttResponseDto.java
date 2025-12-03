package com.ssafy.s13p21b204.global.fastapi.dto;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonIgnoreProperties(ignoreUnknown = true)
public record AiSttResponseDto(
    @JsonProperty("status")
    String status,
    
    @JsonProperty("converted_text")
    String convertedText
) {

}
