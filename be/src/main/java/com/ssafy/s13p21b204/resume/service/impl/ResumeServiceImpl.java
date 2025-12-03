package com.ssafy.s13p21b204.resume.service.impl;

import com.ssafy.s13p21b204.answer.entity.Answer;
import com.ssafy.s13p21b204.answer.repository.AnswerRepository;
import com.ssafy.s13p21b204.global.entity.ProgressStatus;
import com.ssafy.s13p21b204.global.entity.Status;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.jobPosting.entity.JobPosting;
import com.ssafy.s13p21b204.jobPosting.repository.JobPostingRepository;
import com.ssafy.s13p21b204.resume.dto.ResumeDetailResponseDto;
import com.ssafy.s13p21b204.resume.dto.ResumeDetailWrapperDto;
import com.ssafy.s13p21b204.resume.dto.ResumeRequestDto;
import com.ssafy.s13p21b204.resume.dto.ResumeResponseDto;
import com.ssafy.s13p21b204.resume.dto.ResumeWithInterviewIdProjection;
import com.ssafy.s13p21b204.resume.entity.Resume;
import com.ssafy.s13p21b204.resume.event.ResumeCreatedEvent;
import com.ssafy.s13p21b204.resume.repository.ResumeRepository;
import com.ssafy.s13p21b204.resume.service.ResumeService;
import java.util.List;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Slf4j
public class ResumeServiceImpl implements ResumeService {

    private final ResumeRepository resumeRepository;
    private final JobPostingRepository jobPostingRepository;
    private final AnswerRepository answerRepository;
    private final ApplicationEventPublisher eventPublisher;

    @Override
    @Transactional
    public void registerResume(Long userId, ResumeRequestDto resumeRequestDto) {
        log.info("[ResumeService] 자소서 등록 시도");

        // 채용공고 존재 여부 및 상태 확인
        JobPosting jobPosting = jobPostingRepository.findById(resumeRequestDto.jobPostingId())
            .orElseThrow(() -> {
                log.warn("[ResumeService] 자소서 등록 실패 - 채용공고 없음");
                return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.JOB_POSTING_NOT_FOUND);
            });

        if (!jobPosting.getStatus().equals(Status.ACTIVE)) {
            log.warn("[ResumeService] 자소서 등록 실패 - 만료된 채용공고");
            throw ApiException.of(HttpStatus.GONE, ErrorMessage.JOB_POSTING_EXPIRED);
        }

        // 자소서 생성 및 저장
        Resume savedResume = resumeRepository.save(
            Resume.builder()
                .userId(userId)
                .jobPosting(jobPosting)
                .build()
        );

        eventPublisher.publishEvent(
            new ResumeCreatedEvent(
                savedResume.getResumeId(),
                userId,
                resumeRequestDto.jobPostingId(),
                resumeRequestDto.answerRequestDtos()
            )
        );

        log.info("[ResumeService] 자소서 등록 완료 - resumeId={}, 백그라운드 처리 시작", 
            savedResume.getResumeId());
    }

    @Override
    @Transactional(readOnly = true)
    public List<ResumeResponseDto> getResumes(Long userId) {
        log.info("[ResumeService] 유저 전체 자소서 목록 호출 시도");
        
        // DTO Projection을 사용하여 타입 안전하게 조회
        List<ResumeWithInterviewIdProjection> results = 
            resumeRepository.findByUserIdWithInterviewId(userId);
        
        log.info("[ResumeService] 유저 전체 자소서 목록 호출 총: {}개", results.size());
        
        return results.stream()
            .map(projection -> {
                Resume resume = projection.getResume();
                Long interviewId = projection.getInterviewId();
                ProgressStatus progressStatus = projection.getProgressStatus();
                
                // 서비스 레이어에서 데이터 무결성 검증
                if (resume.getJobPosting() == null) {
                    log.error("[ResumeService] 자소서 데이터 무결성 오류 - resumeId={}, jobPosting이 null", 
                        resume.getResumeId());
                    throw ApiException.of(
                        org.springframework.http.HttpStatus.INTERNAL_SERVER_ERROR,
                        "자소서 데이터가 올바르지 않습니다."
                    );
                }
                if (resume.getJobPosting().getCompany() == null) {
                    log.error("[ResumeService] 자소서 데이터 무결성 오류 - resumeId={}, company가 null", 
                        resume.getResumeId());
                    throw ApiException.of(
                        org.springframework.http.HttpStatus.INTERNAL_SERVER_ERROR,
                        "채용공고 데이터가 올바르지 않습니다."
                    );
                }
                
                return ResumeResponseDto.from(resume, interviewId, progressStatus);
            })
            .toList();
    }

  @Override
  @Transactional(readOnly = true)
  public ResumeDetailWrapperDto getResume(Long userId, Long resumeId) {
      log.info("[ResumeService] 자소서 상세 조회 시도");
      
      // 삭제되지 않은 자소서만 조회 (@SQLRestriction이 자동으로 적용됨)
      Resume resume = resumeRepository.findByIdWithJobPostingAndCompany(resumeId).orElseThrow(()->{
        log.warn("[ResumeService] 자소서 상세 조회 실패 - 자소서 없음 또는 삭제됨");
        return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.RESUME_NOT_FOUND);
      });
      
      if(!userId.equals(resume.getUserId())) {
        log.warn("[ResumeService] 자소서 상세 조회 실패 - 권한 없음");
        throw ApiException.of(HttpStatus.FORBIDDEN, ErrorMessage.ACCESS_DENIED);
      }
      
      List<Answer> answers = answerRepository.findByResumeId(resume.getResumeId());
      log.info("[ResumeService] 자소서 상세 조회 - 답변 조회 총: {}개", answers.size());

      // 서비스 레이어에서 데이터 무결성 검증 및 Answer 리스트를 ResumeDetailResponseDto 리스트로 변환
      List<ResumeDetailResponseDto> answerDtos = answers.stream()
          .map(answer -> {
              // Answer의 Question이 null인 경우 데이터 무결성 오류
              if (answer.getQuestion() == null) {
                  log.error("[ResumeService] 자소서 상세 조회 오류 - Answer의 Question이 null, answerId={}", 
                      answer.getAnswerId());
                  throw ApiException.of(
                      HttpStatus.INTERNAL_SERVER_ERROR,
                      "답변 데이터가 올바르지 않습니다."
                  );
              }
              return ResumeDetailResponseDto.from(answer);
          })
          .toList();

      log.info("[ResumeService] 자소서 상세 조회 완료");
      return new ResumeDetailWrapperDto(answerDtos, resume.getCreatedAt());
  }

  @Override
  @Transactional
  public void deleteResume(Long userId, Long resumeId) {
      log.info("[ResumeService] 자소서 삭제 시도 ");
      
      // 삭제되지 않은 자소서만 조회 (@SQLRestriction이 자동으로 적용됨)
      // 삭제 로직에서는 userId만 확인하면 되므로 단순 조회 사용
      Resume resume = resumeRepository.findById(resumeId)
          .orElseThrow(() -> {
              log.warn("[ResumeService] 자소서 삭제 실패 - 자소서 없음 또는 이미 삭제됨");
              return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.RESUME_NOT_FOUND);
          });
      
      // 본인의 자소서인지 확인
      if (!userId.equals(resume.getUserId())) {
          log.warn("[ResumeService] 자소서 삭제 실패 - 권한 없음");
          throw ApiException.of(HttpStatus.FORBIDDEN, ErrorMessage.ACCESS_DENIED);
      }
      
      // 소프트 딜리트 수행 (@SQLDelete가 자동으로 UPDATE 쿼리 실행)
      resumeRepository.delete(resume);
      
      log.info("[ResumeService] 자소서 삭제 완료 ");
  }
}
