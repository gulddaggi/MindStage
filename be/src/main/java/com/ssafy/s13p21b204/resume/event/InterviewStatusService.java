
package com.ssafy.s13p21b204.resume.event;

import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.interview.entity.Interview;
import com.ssafy.s13p21b204.interview.repository.InterviewRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;

/**
 * 면접 상태 전환 책임 서비스.
 */
@Service
@RequiredArgsConstructor
@Slf4j
public class InterviewStatusService {

  private final InterviewRepository interviewRepository;

  @Transactional
  public void markReady(Long interviewId) {
    log.info("[InterviewStatusService] 면접 상태 NOT_STARTED 전환 시도 - interviewId={}", interviewId);
    Interview interview = interviewRepository.findById(interviewId)
        .orElseThrow(() -> {
          log.warn("[InterviewStatusService] 면접 상태 변경 실패 - Interview 미존재 interviewId={}",
              interviewId);
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
        });
    interview.markReady();
    log.info("[InterviewStatusService] 면접 상태 NOT_STARTED 전환 완료 - interviewId={}", interviewId);
  }

  @Transactional(propagation = Propagation.REQUIRES_NEW)
  public void markFailed(Long interviewId) {
    log.info("[InterviewStatusService] 면접 상태 FAILED 전환 시도 - interviewId={}", interviewId);
    Interview interview = interviewRepository.findById(interviewId)
        .orElseThrow(() -> {
          log.warn("[InterviewStatusService] FAILED 전환 실패 - Interview 미존재 interviewId={}",
              interviewId);
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
        });
    interview.markAsFailed();
    log.info("[InterviewStatusService] 면접 상태 FAILED 전환 완료 - interviewId={}", interviewId);
  }

  @Transactional
  public void markFailedByResumeId(Long resumeId) {
    log.info("[InterviewStatusService] Resume 기반 FAILED 전환 시도 - resumeId={}", resumeId);
    interviewRepository.findByResumeResumeId(resumeId)
        .ifPresent(interview -> {
          interview.markAsFailed();
          log.info("[InterviewStatusService] Resume 기반 FAILED 전환 완료 - interviewId={}",
              interview.getInterviewId());
        });
  }
}