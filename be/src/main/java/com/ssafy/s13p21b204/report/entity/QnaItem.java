package com.ssafy.s13p21b204.report.entity;

import java.util.ArrayList;
import java.util.List;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 면접 Q&A 항목 (질문, 답변, 평가 라벨)
 * Report 엔티티의 qnaList에서 사용
 * 일반 질문: question 필드 사용
 * 꼬리질문: relatedQuestion 필드 사용
 */
@Getter
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class QnaItem {

  /**
   * 면접 질문 내용 (일반 질문인 경우 사용)
   */
  private String question;

  /**
   * 꼬리질문 내용 (꼬리질문인 경우 사용)
   */
  private String relatedQuestion;

  /**
   * 면접 답변 내용
   */
  private String answer;

  /**
   * 답변에 대한 평가 라벨 리스트
   * 2: 부정, 1: 긍정, 0: 중립
   */
  @Builder.Default
  private List<Integer> labels = new ArrayList<>();
}

