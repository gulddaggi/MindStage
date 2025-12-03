package com.ssafy.s13p21b204.report.dto;

import com.ssafy.s13p21b204.heartBeat.dto.BpmWithMeasureAtDto;
import com.ssafy.s13p21b204.report.entity.QnaItem;
import io.swagger.v3.oas.annotations.media.Schema;
import java.util.List;
import java.util.Map;

@Schema(description = "레포트 상세 응답 DTO")
public record ReportResponseDetailDto(
    @Schema(description = "면접에 대한 총평", example = "전반적으로 좋은 면접이었습니다. 의사소통 능력이 뛰어나며...")
    String comment,
    
    @Schema(description = "면접 중 측정된 심박수 리스트 (BPM)", example = "[72, 75, 78, 80, 82]")
    List<BpmWithMeasureAtDto> heartBeats,
    
    @Schema(description = "5가지 평가 항목별 점수", example = "{\"의사소통\": 85, \"진실성\": 90, \"적응성\": 80, \"대인관계\": 88, \"팀워크\": 82}")
    Map<String, Integer> myScores,
    
    @Schema(description = "같은 채용공고에 지원한 다른 지원자들의 평균 점수", example = "{\"의사소통\": 78, \"진실성\": 82, \"적응성\": 75, \"대인관계\": 80, \"팀워크\": 79}")
    Map<String, Integer> averageScores,
    
    @Schema(description = "면접 중 질의응답 리스트 (각 QnaItem은 question 또는 relatedQuestion, answer, labels를 포함하며, 각 answer에 대응하는 labels 리스트가 매핑됨). 일반 질문은 question 필드 사용, 꼬리질문은 relatedQuestion 필드 사용", 
        example = "[{\"question\": \"자기소개를 해주세요.\", \"relatedQuestion\": null, \"answer\": \"안녕하세요. 저는...\", \"labels\": [1]}, {\"question\": null, \"relatedQuestion\": \"그 경험을 통해 무엇을 배우셨나요?\", \"answer\": \"저는...\", \"labels\": [0]}]")
    List<QnaItem> qnaList
) {

}
