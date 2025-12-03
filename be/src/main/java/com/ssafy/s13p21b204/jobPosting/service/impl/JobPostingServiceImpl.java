package com.ssafy.s13p21b204.jobPosting.service.impl;

import com.ssafy.s13p21b204.company.entity.Company;
import com.ssafy.s13p21b204.company.repository.CompanyRepository;
import com.ssafy.s13p21b204.global.entity.Status;
import com.ssafy.s13p21b204.global.event.S3TicketConsumeEvent;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.util.S3Util;
import com.ssafy.s13p21b204.jobPosting.dto.JobPostingRegisterDto;
import com.ssafy.s13p21b204.jobPosting.dto.JobPostingResponseDto;
import com.ssafy.s13p21b204.jobPosting.entity.JobPosting;
import com.ssafy.s13p21b204.jobPosting.repository.JobPostingRepository;
import com.ssafy.s13p21b204.jobPosting.service.JobPostingService;
import com.ssafy.s13p21b204.question.dto.QuestionRequestDto;
import com.ssafy.s13p21b204.question.entity.Question;
import com.ssafy.s13p21b204.question.repository.QuestionRepository;
import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@Slf4j
@RequiredArgsConstructor
public class JobPostingServiceImpl implements JobPostingService {

  private final JobPostingRepository jobPostingRepository;
  private final CompanyRepository companyRepository;
  private final QuestionRepository questionRepository;
  private final S3Util s3Util;
  private final ApplicationEventPublisher applicationEventPublisher;

  @Override
  @Transactional
  public void createJobPosting(JobPostingRegisterDto jobPostingRegisterDto) {
    log.info("[JobPostingService] 채용 공고 등록 시도");
    if (jobPostingRegisterDto.s3PreferenceFileKey() != null
        && !jobPostingRegisterDto.s3PreferenceFileKey().isBlank()) {
      s3Util.validateS3Ticket(jobPostingRegisterDto.s3PreferenceFileKey());
      log.info("[JobPostingService] S3 업로드 티켓 검증 완료");
    }

    // 회사 존재 여부 확인
    Company company = companyRepository.findById(jobPostingRegisterDto.companyId())
        .orElseThrow(() -> {
          log.warn("[JobPostingService] 채용 공고 등록 실패 - 존재하지 않는 기업");
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.COMPANY_NOT_FOUND);
        });

    log.info("[JobPostingService] 채용 공고 등록 진행 ");

    // 같은 회사의 같은 직무에 대해서만 중복 체크
    Optional<JobPosting> existingJobPosting = jobPostingRepository
        .findByCompany_CompanyIdAndPartAndStatus(
            jobPostingRegisterDto.companyId(),
            jobPostingRegisterDto.part(),
            Status.ACTIVE
        );

    if (existingJobPosting.isPresent()) {
      log.warn("[JobPostingService] 채용 공고 등록 실패 - 이미 진행 중인 채용공고 존재 ");
      throw ApiException.of(HttpStatus.CONFLICT, ErrorMessage.JOB_POSTING_ALREADY_EXISTS);
    }

    // 질문 검증
    if (jobPostingRegisterDto.questions() == null || jobPostingRegisterDto.questions().isEmpty()) {
      log.warn("[JobPostingService] 채용 공고 등록 실패 - 질문이 없음");
      throw ApiException.of(HttpStatus.UNPROCESSABLE_ENTITY, ErrorMessage.QUESTION_LIST_EMPTY);
    }

    JobPosting jobPosting = JobPosting.builder()
        .createdAt(jobPostingRegisterDto.createdAt())
        .status(Status.ACTIVE)
        .company(company)
        .expiredAt(jobPostingRegisterDto.expiredAt())
        .part(jobPostingRegisterDto.part())
        .s3PreferenceFileKey(jobPostingRegisterDto.s3PreferenceFileKey())
        .build();

    JobPosting savedJobPosting = jobPostingRepository.save(jobPosting);
    log.info("[JobPostingService] 채용 공고 등록 완료 ");

    List<Question> questions = new ArrayList<>();

    for (QuestionRequestDto questionDto : jobPostingRegisterDto.questions()) {
      Question question = Question.builder()
          .jobPosting(savedJobPosting)
          .content(questionDto.question())
          .limitCnt(questionDto.limit())
          .build();
      questions.add(question);
    }
    questionRepository.saveAll(questions);
    log.info("[JobPostingService] 질문 등록 완료 - 총 {}개", questions.size());

    if (jobPostingRegisterDto.s3PreferenceFileKey() != null
        && !jobPostingRegisterDto.s3PreferenceFileKey().isBlank()) {
      applicationEventPublisher.publishEvent(
          new S3TicketConsumeEvent(jobPostingRegisterDto.s3PreferenceFileKey())
      );
      log.info("[JobPostingService] S3 티켓 소비 이벤트 발행");
    }
  }

  @Override
  public S3Util.S3UploadInfo generatePreferenceFileUploadUrl(String fileName) {
    log.info("[JobPostingService] 직무 우대사항 파일 업로드 URL 발급 시도");

    // S3Util을 사용하여 Presigned URL 발급 (Redis 티켓 자동 생성)
    S3Util.S3UploadInfo uploadInfo = s3Util.generateUploadPresignedUrl(
        "job-postings/preferences",  // S3 디렉토리 경로
        fileName
    );

    log.info("[JobPostingService] 업로드 URL 발급 완료");
    return uploadInfo;
  }

  @Override
  @Transactional(readOnly = true)
  public List<JobPostingResponseDto> findAll() {
    log.info("[JobPostingService] 진행 중인 채용 공고 조회 시도");

    List<JobPosting> jobPostings = jobPostingRepository.findActiveJobPostingsWithQuestions(
        LocalDateTime.now());

    log.info("[JobPostingService] 진행 중인 채용 공고 조회 결과 총 {}개", jobPostings.size());

    return jobPostings.stream()
        .map(jobPosting -> {
          return JobPostingResponseDto.of(jobPosting);
        })
        .toList();
  }
}