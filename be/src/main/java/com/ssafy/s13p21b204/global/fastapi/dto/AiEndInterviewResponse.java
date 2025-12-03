package com.ssafy.s13p21b204.global.fastapi.dto;

import java.util.List;
import java.util.Map;
import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import lombok.Data;

@Data
@JsonIgnoreProperties(ignoreUnknown = true)
public class AiEndInterviewResponse {

    private String status;
    private Map<String, Integer> scores;
    private List<Integer> labels;
    private String report;
}


