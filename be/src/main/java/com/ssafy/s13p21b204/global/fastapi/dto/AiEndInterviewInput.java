package com.ssafy.s13p21b204.global.fastapi.dto;

import com.fasterxml.jackson.annotation.JsonProperty;
import java.util.List;
import java.util.Map;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class AiEndInterviewInput {

    @JsonProperty("jd_presigned_url")
    private String jd;
    private List<AiResumeQAItem> resume;

    @JsonProperty("qna_history")
    private List<Map<String, String>> qnaHistory;

    @JsonProperty("preflight_urls")
    private List<String> preflightUrls;
}


