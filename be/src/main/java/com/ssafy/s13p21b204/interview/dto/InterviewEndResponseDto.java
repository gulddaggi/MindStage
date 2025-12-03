package com.ssafy.s13p21b204.interview.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import java.util.List;
import java.util.Map;

@Schema(description = "면접 종료 응답 DTO")
public record InterviewEndResponseDto(
    @Schema(description = "AI 응답 상태", example = "success")
    String status,
    
    @Schema(description = "5가지 평가 항목별 점수", example = "{\"의사소통\": 85, \"진실성\": 90, \"적응성\": 80, \"대인관계\": 88, \"팀워크\": 82}")
    Map<String, Integer> scores,
    
    @Schema(description = "각 문항에 대한 긍정,부정,중립 평가", example = "{1,0,2,0,1}")
    List<Integer> labels,
    
    @Schema(description = "전반적인 면접 총평", example = "전반적으로 좋은 면접이었습니다...")
    String report
) {}


