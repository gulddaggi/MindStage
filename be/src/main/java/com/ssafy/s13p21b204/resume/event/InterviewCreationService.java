
package com.ssafy.s13p21b204.resume.event;

import com.ssafy.s13p21b204.global.entity.ProgressStatus;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.interview.entity.Interview;
import com.ssafy.s13p21b204.interview.repository.InterviewRepository;
import com.ssafy.s13p21b204.resume.entity.Resume;
import com.ssafy.s13p21b204.resume.repository.ResumeRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 면접 엔티티 생성 단계 전용 서비스.
 */
@Service
@RequiredArgsConstructor
@Slf4j
public class InterviewCreationService {

  private final ResumeRepository resumeRepository;
  private final InterviewRepository interviewRepository;

  @Transactional
  public Long createInterview(Long resumeId) {
    log.info("[InterviewCreationService] 면접 생성 시작 ");

    Resume resume = resumeRepository.findById(resumeId)
        .orElseThrow(() -> {
          log.warn("[InterviewCreationService] 면접 생성 실패 - 자소서 없음");
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.RESUME_NOT_FOUND);
        });

    // Resume를 통해 Company ID 조회
    Long companyId = null;
    if (resume.getJobPosting() != null && resume.getJobPosting().getCompany() != null) {
      companyId = resume.getJobPosting().getCompany().getCompanyId();
    }

    Interview interview = Interview.builder()
        .resume(resume)
        .companyId(companyId)  // 일반 면접은 Resume를 통해 companyId 저장
        .progressStatus(ProgressStatus.CREATING)
        .relatedQuestion(false)
        .build();

    Interview saved = interviewRepository.save(interview);
    log.info("[InterviewCreationService] 면접 생성 완료 ");
    return saved.getInterviewId();
  }
}