
package com.ssafy.s13p21b204.resume.event;

import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.util.S3Util;
import com.ssafy.s13p21b204.interview.entity.InterviewQuestion;
import com.ssafy.s13p21b204.interview.repository.InterviewQuestionRepository;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 생성된 면접 질문의 S3 업로드 상태를 검증한다.
 */
@Service
@RequiredArgsConstructor
@Slf4j
public class InterviewQuestionValidationService {

  private static final int REQUIRED_QUESTION_COUNT = 5;

  private final InterviewQuestionRepository interviewQuestionRepository;
  private final S3Util s3Util;

  @Transactional
  public void validate(Long interviewId, List<InterviewQuestion> questions) {
    log.info("[InterviewQuestionValidationService] S3 검증 시작 - interviewId={}, 질문 수={}",
        interviewId, questions.size());

    Map<Long, Boolean> existenceMap = new ConcurrentHashMap<>();
    questions.parallelStream().forEach(question -> {
      boolean exists = s3Util.doesObjectExist(question.getS3Key());
      existenceMap.put(question.getInterviewQuestionId(), exists);
    });

    List<Long> missingQuestionIds = questions.stream()
        .filter(question -> !existenceMap.getOrDefault(question.getInterviewQuestionId(), false))
        .map(InterviewQuestion::getInterviewQuestionId)
        .collect(Collectors.toList());

    if (!missingQuestionIds.isEmpty() || questions.size() < REQUIRED_QUESTION_COUNT) {
      log.error("[InterviewQuestionValidationService] S3 검증 실패 - interviewId={}, 누락 질문={}",
          interviewId, missingQuestionIds);
      if (!missingQuestionIds.isEmpty()) {
        interviewQuestionRepository.deleteAllById(missingQuestionIds);
      }
      throw ApiException.of(HttpStatus.UNPROCESSABLE_ENTITY,
          ErrorMessage.S3_FILE_VALIDATION_FAILED);
    }

    log.info("[InterviewQuestionValidationService] S3 검증 완료 - interviewId={}", interviewId);
  }
}