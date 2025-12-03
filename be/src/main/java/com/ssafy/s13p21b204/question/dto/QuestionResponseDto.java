package com.ssafy.s13p21b204.question.dto;

import com.ssafy.s13p21b204.question.entity.Question;

public record QuestionResponseDto(
    Long questionId,
    String question,
    int limit
) {
  public static QuestionResponseDto of(Question question) {
    return new QuestionResponseDto(
        question.getQuestionId(),
        question.getContent(),
        question.getLimitCnt()
    );
  }

}
