
package com.ssafy.s13p21b204.resume.event;

import com.ssafy.s13p21b204.answer.dto.AnswerRequestDto;
import com.ssafy.s13p21b204.answer.service.AnswerService;
import java.util.List;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 자소서 답변 저장 단계 전용 서비스.
 */
@Service
@RequiredArgsConstructor
@Slf4j
public class AnswerRegistrationService {

  private final AnswerService answerService;

  @Transactional
  public void registerAnswers(Long resumeId, List<AnswerRequestDto> answers) {
    log.info("[AnswerRegistrationService] 자소서 답변 일괄 등록 시작 ");
    answerService.registerAll(resumeId, answers);
    log.info("[AnswerRegistrationService] 자소서 답변 일괄 등록 완료 ");
  }
}