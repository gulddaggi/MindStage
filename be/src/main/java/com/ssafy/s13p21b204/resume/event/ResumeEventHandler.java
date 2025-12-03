package com.ssafy.s13p21b204.resume.event;

import com.ssafy.s13p21b204.global.exception.ApiException;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Async;
import org.springframework.stereotype.Component;
import org.springframework.transaction.event.TransactionPhase;
import org.springframework.transaction.event.TransactionalEventListener;

/**
 * Resume 생성 이벤트 처리 Resume - Answer 등록 - Interview 생성 - FastAPI 호출 - InterviewQuestion 생성 -
 * Interview상태 변경
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class ResumeEventHandler {

  private final AnswerRegistrationService answerRegistrationService;
  private final InterviewCreationService interviewCreationService;
  private final InterviewQuestionCreationService interviewQuestionCreationService;
  private final InterviewStatusService interviewStatusService;

  /**
   * Resume 생성 이후 면접 워크플로우를 오케스트레이션한다.
   */
  @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
  @Async("interviewWorkflowExecutor")
  public void handleResumeCreated(ResumeCreatedEvent event) {
    log.info("[ResumeEventHandler] Resume 생성 이벤트 수신 ");

    Long interviewId = null;
    try {

      answerRegistrationService.registerAnswers(event.resumeId(), event.answers());
      interviewId = interviewCreationService.createInterview(event.resumeId());
      interviewQuestionCreationService.createQuestions(interviewId, event);
      interviewStatusService.markReady(interviewId);
      log.info("[ResumeEventHandler] Resume 기반 면접 워크플로우 완료");
    } catch (
        ApiException apiException) {
      log.warn("[ResumeEventHandler] 워크플로우 실패(ApiException) ");
      handleFailure(event, interviewId);
    } catch (
        Exception exception) {
      log.error("[ResumeEventHandler] 워크플로우 실패(Unexpected) ");
      handleFailure(event, interviewId);
    }
  }

  private void handleFailure(ResumeCreatedEvent event, Long interviewId) {
    if (interviewId != null) {
      interviewStatusService.markFailed(interviewId);
    } else {
      interviewStatusService.markFailedByResumeId(event.resumeId());
    }

  }
}

