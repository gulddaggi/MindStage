package com.ssafy.s13p21b204.answer.service.impl;

import com.ssafy.s13p21b204.answer.dto.AnswerRequestDto;
import com.ssafy.s13p21b204.answer.entity.Answer;
import com.ssafy.s13p21b204.answer.repository.AnswerRepository;
import com.ssafy.s13p21b204.answer.service.AnswerService;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.question.entity.Question;
import com.ssafy.s13p21b204.question.repository.QuestionRepository;
import java.util.List;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@Slf4j
@RequiredArgsConstructor
public class AnswerServiceImpl implements AnswerService {

  private final AnswerRepository answerRepository;
  private final QuestionRepository questionRepository;

  @Override
  @Transactional
  public void registerAll(Long resumeId, List<AnswerRequestDto> answerRequestDtos) {
    log.info("[AnswerService] 답변 등록 시도, 총: {}개", answerRequestDtos.size());
    
    int cnt = 0;
    for (AnswerRequestDto answerRequestDto : answerRequestDtos) {
      // 질문 존재 여부 확인
      Question question = questionRepository.findById(answerRequestDto.questionId())
          .orElseThrow(() -> {
            log.warn("[AnswerService] 답변 등록 실패 - 질문 없음");
            return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.QUESTION_NOT_FOUND);
          });
      
      // 글자수 검증
      validateAnswerLength(answerRequestDto.content(), question);
      
      // 답변 생성 및 저장
      Answer answer = Answer.builder()
          .question(question)
          .resumeId(resumeId)
          .content(answerRequestDto.content())
          .build();
      answerRepository.save(answer);
      
      log.info("[AnswerService] 답변 등록 {}/{}", ++cnt, answerRequestDtos.size());
    }
  }

  /**
   * 답변 글자수 유효성 검증
   * 각 질문의 limitCnt 기준으로 검증
   */
  private void validateAnswerLength(String content, Question question) {
    int contentLength = content.length();
    int maxLimit = question.getLimitCnt();
    
    // 최소 글자수 검증
    if (contentLength < 10) {
      log.warn("[AnswerService] 답변 글자수 부족 - 작성: {}자", contentLength);
      throw ApiException.of(HttpStatus.UNPROCESSABLE_ENTITY, ErrorMessage.ANSWER_TOO_SHORT);
    }
    
    // 최대 글자수 검증 (각 질문의 limitCnt 기준)
    if (contentLength > maxLimit) {
      log.warn("[AnswerService] 답변 글자수 초과 - 작성: {}자, 제한: {}자", 
               contentLength, maxLimit);
      throw ApiException.of(HttpStatus.BAD_REQUEST, 
          String.format("답변은 최대 %d자까지 작성 가능합니다. (현재: %d자)", maxLimit, contentLength));
    }
  }
}
