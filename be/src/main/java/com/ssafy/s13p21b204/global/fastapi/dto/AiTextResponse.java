package com.ssafy.s13p21b204.global.fastapi.dto;

import lombok.Data;
import com.fasterxml.jackson.annotation.JsonIgnoreProperties;

@Data
@JsonIgnoreProperties(ignoreUnknown = true)
public class AiTextResponse {

    private String status;
    private String text;
}


