package com.ssafy.s13p21b204.interview.service.impl;

import com.ssafy.s13p21b204.answer.entity.Answer;
import com.ssafy.s13p21b204.answer.repository.AnswerRepository;
import com.ssafy.s13p21b204.global.entity.ProgressStatus;
import com.ssafy.s13p21b204.global.fastapi.AiClient;
import com.ssafy.s13p21b204.global.fastapi.dto.AiEndInterviewInput;
import com.ssafy.s13p21b204.global.fastapi.dto.AiEndInterviewResponse;
import com.ssafy.s13p21b204.global.fastapi.dto.AiInterviewInput;
import com.ssafy.s13p21b204.global.fastapi.dto.AiInterviewResponse;
import com.ssafy.s13p21b204.global.fastapi.dto.AiResumeQAItem;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.util.S3Util;
import com.ssafy.s13p21b204.global.util.S3Util.S3UploadInfo;
import com.ssafy.s13p21b204.interview.dto.DemoInterviewResponseDto;
import com.ssafy.s13p21b204.interview.dto.InterviewEndRequestDto;
import com.ssafy.s13p21b204.interview.dto.InterviewQuestionResponseDto;
import com.ssafy.s13p21b204.interview.dto.InterviewReplyRequestDto;
import com.ssafy.s13p21b204.interview.dto.RelatedQuestionResponseDto;
import com.ssafy.s13p21b204.company.entity.Company;
import com.ssafy.s13p21b204.company.repository.CompanyRepository;
import com.ssafy.s13p21b204.jobPosting.repository.JobPostingRepository;
import java.util.Comparator;
import com.ssafy.s13p21b204.interview.entity.Interview;
import com.ssafy.s13p21b204.interview.entity.InterviewQuestion;
import com.ssafy.s13p21b204.interview.entity.Reply;
import com.ssafy.s13p21b204.interview.event.InterviewEndedEvent;
import com.ssafy.s13p21b204.interview.repository.InterviewQuestionRepository;
import com.ssafy.s13p21b204.interview.repository.InterviewRepository;
import com.ssafy.s13p21b204.interview.repository.ReplyRepository;
import com.ssafy.s13p21b204.interview.service.InterviewService;
import com.ssafy.s13p21b204.jobPosting.entity.JobPosting;
import com.ssafy.s13p21b204.report.service.ReportService;
import com.ssafy.s13p21b204.resume.entity.Resume;
import com.ssafy.s13p21b204.resume.event.InterviewStatusService;
import com.ssafy.s13p21b204.resume.repository.ResumeRepository;
import com.ssafy.s13p21b204.notification.service.FirebasePushService;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@Slf4j
@RequiredArgsConstructor
public class InterviewServiceImpl implements InterviewService {

  private final InterviewRepository interviewRepository;
  private final ResumeRepository resumeRepository;
  private final InterviewQuestionRepository interviewQuestionRepository;
  private final ReplyRepository replyRepository;
  private final AnswerRepository answerRepository;
  private final AiClient aiClient;
  private final S3Util s3Util;
  private final ApplicationEventPublisher eventPublisher;
  private final ReportService reportService;
  private final InterviewStatusService interviewStatusService;
  private final FirebasePushService firebasePushService;
  private final CompanyRepository companyRepository;
  private final JobPostingRepository jobPostingRepository;


  @Override
  @Transactional
  public List<InterviewQuestionResponseDto> getQuestionPresignedUrls(Long interviewId,
      Long userId) {
    log.info("[InterviewService] 질문 Presigned URL 조회 시도 - interviewId={}, userId={}", interviewId,
        userId);

    // 면접 존재 확인
    Interview interview = interviewRepository.findById(interviewId)
        .orElseThrow(() -> {
          log.warn("[InterviewService] 질문 조회 실패 - 면접 없음 ");
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
        });

    // 권한 확인: 본인의 면접인지 검증
    Resume resume = interview.getResume();
    if (resume != null) {
      // 기존 로직: Resume가 있을 때 (일반 면접)
      if (!resume.getUserId().equals(userId)) {
        log.warn("[InterviewService] 질문 조회 실패 - 접근 권한 없음 ");
        throw ApiException.of(HttpStatus.FORBIDDEN, ErrorMessage.ACCESS_DENIED);
      }
    } else {
      // 시연용 면접: Resume가 null일 때 - 권한 검증 생략
      log.info("[InterviewService] 시연용 면접 - 권한 검증 생략");
    }

    // 상태 확인: NOT_STARTED 상태일 때만 동작
    if (interview.getProgressStatus() != ProgressStatus.NOT_STARTED) {
      log.warn("[InterviewService] 질문 조회 실패 - 시작 불가 상태: {} (interviewId: {})",
          interview.getProgressStatus(), interviewId);
      throw ApiException.of(HttpStatus.CONFLICT, ErrorMessage.INTERVIEW_NOT_READY);
    }

    // InterviewQuestion 조회
    List<InterviewQuestion> questions = interviewQuestionRepository.findByInterviewId(interviewId);

    if (questions.isEmpty()) {
      log.warn("[InterviewService] 질문 조회 실패 - 질문 없음 (interviewId: {})", interviewId);
      throw ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_QUESTIONS_NOT_FOUND);
    }

    // Presigned URL 생성 및 DTO 변환
    List<InterviewQuestionResponseDto> responseDtoList = questions.stream()
        .map(question -> {
          String presignedUrl = s3Util.generateDownloadPresignedUrl(question.getS3Key());
          return new InterviewQuestionResponseDto(
              question.getInterviewQuestionId(),
              presignedUrl,
              question.getDifficult()
          );
        })
        .collect(Collectors.toList());

    // 면접 상태를 IN_PROGRESS로 변경
    interview.start();
    interviewRepository.save(interview);

    // 워치에 건강데이터 전송 요청 푸시 전송 (인터뷰 시작 시점)
    try {
      int durationSec = 20 * 60; // 기본 20분
      firebasePushService.sendHealthRequest(userId, interviewId, durationSec);
      log.info("[InterviewService] 건강데이터 요청 푸시 전송 완료 - interviewId={}, userId={}, durationSec={}",
          interviewId, userId, durationSec);
    } catch (Exception e) {
      log.warn("[InterviewService] 건강데이터 요청 푸시 전송 실패 - interviewId={}, userId={}, err={}",
          interviewId, userId, e.getMessage());
    }

    log.info("[InterviewService] 질문 Presigned URL 조회 완료 및 면접 상태 변경 - interviewId={}, 질문 수={}",
        interviewId, responseDtoList.size());

    return responseDtoList;
  }

  @Transactional
  @Override
  public void registerReply(Long userId, InterviewReplyRequestDto interviewReplyRequestDto) {
    log.info("[InterviewService] 꼬리질문 없는 답변 등록 시도 - questionId={}, s3Key={}",
        interviewReplyRequestDto.questionId(), interviewReplyRequestDto.s3Key());

    // Step 1: Redis 티켓 검증 (s3Key가 유효한 업로드 티켓인지 확인)
    s3Util.validateS3Ticket(interviewReplyRequestDto.s3Key());
    log.info("[InterviewService] S3 업로드 티켓 검증 완료 - s3Key: {}", interviewReplyRequestDto.s3Key());

    // Step 2: InterviewQuestion 조회
    InterviewQuestion parentQuestion = interviewQuestionRepository.findById(
        interviewReplyRequestDto.questionId()).orElseThrow(() -> {
      log.warn("[InterviewService] 꼬리질문 없는 답변 등록 실패 - 질문 없음 (questionId: {})",
          interviewReplyRequestDto.questionId());
      return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.QUESTION_NOT_FOUND);
    });

    // Step 3: Interview 조회 및 권한 확인
    Interview interview = interviewRepository.findById(parentQuestion.getInterviewId())
        .orElseThrow(() -> {
          log.warn("[InterviewService] 면접 조회 실패 - interviewId={}", parentQuestion.getInterviewId());
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
        });

    Resume resume = interview.getResume();
    if (resume != null) {
      // 기존 로직: Resume가 있을 때 (일반 면접)
      if (!resume.getUserId().equals(userId)) {
        log.warn("[InterviewService] 답변 등록 실패 - 접근 권한 없음 (userId: {}, resumeUserId: {})",
            userId, resume.getUserId());
        throw ApiException.of(HttpStatus.FORBIDDEN, ErrorMessage.ACCESS_DENIED);
      }
    } else {
      // 시연용 면접: Resume가 null일 때 - 권한 검증 생략
      log.info("[InterviewService] 시연용 면접 - 권한 검증 생략");
    }

    // Step 4: S3 key로 presigned URL 발급 (FastAPI에 전달할 용도)
    String preSignedUrl = s3Util.generateDownloadPresignedUrl(interviewReplyRequestDto.s3Key());
    log.info("[InterviewService] Presigned URL 발급 완료 - s3Key: {}",
        interviewReplyRequestDto.s3Key());

    // Step 5: FastAPI STT 엔드포인트 호출
    String convertedText = aiClient.transcribeAudio(preSignedUrl);
    log.info("[InterviewService] STT 변환 완료 - 변환된 텍스트 길이: {}", convertedText.length());

    // Step 6: Reply 저장 (항상 새로 생성)
    Reply reply = Reply.builder()
        .interviewQuestion(parentQuestion)
        .content(convertedText)
        .s3Key(interviewReplyRequestDto.s3Key())
        .build();

    replyRepository.save(reply);
    log.info("[InterviewService] 꼬리질문 없는 답변 등록 완료 - replyId={}, content 길이={}",
        reply.getReplyId(), convertedText.length());
  }

  @Override
  @Transactional
  public RelatedQuestionResponseDto registerReplyWithRelatedQuestion(Long userId,
      InterviewReplyRequestDto interviewReplyRequestDto) {
    log.info("[InterviewService] 꼬리질문 있는 답변 등록 시도 ");

    // Step 1: InterviewQuestion 조회 및 권한 확인
    InterviewQuestion parentQuestion = interviewQuestionRepository
        .findById(interviewReplyRequestDto.questionId())
        .orElseThrow(() -> {
          log.warn("[InterviewService] 꼬리질문 있는 답변 등록 실패 - 인터뷰 질문 없음");
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_QUESTIONS_NOT_FOUND);
        });

    // Step 2: Interview 조회 및 권한 확인
    Interview interview = interviewRepository.findById(parentQuestion.getInterviewId())
        .orElseThrow(() -> {
          log.warn("[InterviewService] 면접 조회 실패 - interviewId={}", parentQuestion.getInterviewId());
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
        });

    Resume resume = interview.getResume();
    List<AiResumeQAItem> resumeQA = new ArrayList<>();

    if (resume != null) {
      // 기존 로직: Resume가 있을 때 (일반 면접)
      if (!resume.getUserId().equals(userId)) {
        log.warn("[InterviewService] 답변 등록 실패 - 접근 권한 없음");
        throw ApiException.of(HttpStatus.FORBIDDEN, ErrorMessage.ACCESS_DENIED);
      }

      // Step 3: Resume의 Answer 리스트 조회
      List<Answer> answers = answerRepository.findByResumeId(resume.getResumeId());

      // Step 4: Resume Q&A 배열 구성
      for (int i = 0; i < answers.size(); i++) {
        Answer answer = answers.get(i);
        String num = String.valueOf(i + 1);
        String question = answer.getQuestion().getContent();
        String answerContent = answer.getContent() != null ? answer.getContent() : "";

        resumeQA.add(new AiResumeQAItem(
            num,
            question,
            answerContent,
            answerContent
        ));
      }
    } else {
      // 시연용 면접: Resume가 null일 때 - 빈 배열 사용 (FastAPI에서 빈 배열 허용)
      log.info("[InterviewService] 시연용 면접 - 빈 자소서 배열 사용");
      resumeQA = new ArrayList<>();
    }

    // Step 5: qna_history 구성 (이전 질문-답변 쌍들)
    List<InterviewQuestion> allQuestions = interviewQuestionRepository
        .findByInterviewId(interview.getInterviewId());

    // 질문-답변 매핑 생성 (질문 ID -> 답변)
    Map<Long, String> questionAnswerMap = new HashMap<>();
    for (InterviewQuestion question : allQuestions) {
      Optional<Reply> replyOpt = replyRepository
          .findByInterviewQuestionInterviewQuestionId(question.getInterviewQuestionId());
      if (replyOpt.isPresent()) {
        questionAnswerMap.put(question.getInterviewQuestionId(), replyOpt.get().getContent());
      }
    }

    // qna_history 구성 (이전 질문-답변 쌍들 + 현재 질문)
    // 부모 질문들만 포함 (parentQuestionId가 null인 것들)
    List<Map<String, String>> qnaHistory = new ArrayList<>();
    for (InterviewQuestion question : allQuestions) {
      // 부모 질문만 포함 (parentQuestionId가 null인 것들, 현재 질문 제외)
      if (question.getParentQuestionId() == null
          && !question.getInterviewQuestionId().equals(parentQuestion.getInterviewQuestionId())) {
        Map<String, String> qaPair = new HashMap<>();
        qaPair.put("question", question.getContent());
        qaPair.put("answer", questionAnswerMap.getOrDefault(question.getInterviewQuestionId(), ""));
        qnaHistory.add(qaPair);
      }
    }

    // 현재 질문을 qna_history 마지막에 추가 (답변은 빈 문자열, STT 결과가 자동 병합됨)
    Map<String, String> currentQA = new HashMap<>();
    currentQA.put("question", parentQuestion.getContent());
    currentQA.put("answer", "");
    qnaHistory.add(currentQA);

    log.info("[InterviewService] qna_history 구성 완료 - 총 {}개 (현재 질문 포함)", qnaHistory.size());

    // Step 6: Presigned URL 발급
    // latest_wav_file_url: 사용자가 업로드한 오디오 파일 다운로드용
    String latestWavFileUrl = s3Util.generateDownloadPresignedUrl(interviewReplyRequestDto.s3Key());

    // saved_tts_file_url: TTS 파일 업로드용 (1개만 필요)
    S3Util.S3UploadInfo ttsUploadInfo = s3Util.generateUploadPresignedUrl(
        "interviews/questions",
        String.format("interview_%d_related_question_%d.wav",
            interview.getInterviewId(), parentQuestion.getInterviewQuestionId())
    );

    // Step 7: FastAPI answer 엔드포인트 호출
    // null 값들을 빈 문자열/빈 배열로 변환하여 모든 필드 포함
    AiInterviewInput aiInput = new AiInterviewInput(
        "",  // jd_presigned_url은 빈 문자열 (answer 엔드포인트는 JD 사용 안 함)
        resumeQA != null ? resumeQA : new ArrayList<>(),  // resume 배열
        qnaHistory != null ? qnaHistory : new ArrayList<>(),  // qna_history 배열
        latestWavFileUrl != null ? latestWavFileUrl : "",  // latest_wav_file_url (빈 문자열)
        List.of(ttsUploadInfo.presignedUrl())  // saved_tts_file_url 배열
    );

    // FastAPI 요청 바디 로깅 (AiClient에서도 로깅하지만, 여기서도 로깅하여 확인)
    log.info(
        "[InterviewService] FastAPI 요청 준비 완료 - resumeQA 개수: {}, qnaHistory 개수: {}, latestWavFileUrl: {}, savedTtsFileUrl 개수: 1",
        resumeQA.size(), qnaHistory.size(), latestWavFileUrl != null ? "있음" : "없음");

    AiInterviewResponse response = aiClient.answerInterview(aiInput);
    log.info("[InterviewService] FastAPI 응답 수신 - STT: {}, 질문 수: {}",
        response.convertedTextWithStt() != null ? "있음" : "없음",
        response.textFromTts() != null ? response.textFromTts().size() : 0);

    // Step 8: Reply 저장 또는 업데이트
    Optional<Reply> existingReplyOpt = replyRepository
        .findByInterviewQuestionInterviewQuestionId(parentQuestion.getInterviewQuestionId());

    String replyContent =
        response.convertedTextWithStt() != null ? response.convertedTextWithStt() : "";

    Reply reply;
    if (existingReplyOpt.isPresent()) {
      // 기존 Reply 업데이트 (replyId 포함하여 새로 생성)
      Reply existingReply = existingReplyOpt.get();
      reply = Reply.builder()
          .replyId(existingReply.getReplyId())
          .interviewQuestion(parentQuestion)
          .content(replyContent)
          .s3Key(interviewReplyRequestDto.s3Key())
          .createdAt(existingReply.getCreatedAt())  // 생성일시 유지
          .build();
    } else {
      // 새 Reply 생성
      reply = Reply.builder()
          .interviewQuestion(parentQuestion)
          .content(replyContent)
          .s3Key(interviewReplyRequestDto.s3Key())
          .build();
    }
    replyRepository.save(reply);
    log.info("[InterviewService] Reply 저장 완료 - replyId={}, content 길이={}",
        reply.getReplyId(), replyContent.length());

    // Step 9: 자식 InterviewQuestion 생성 (꼬리질문)
    InterviewQuestion childQuestion = null;
    if (response.textFromTts() != null && !response.textFromTts().isEmpty()) {
      String childQuestionText = response.textFromTts().get(0);

      // 면접관 타입 (talker) 매핑: 0 - STRICT, 1 - LAX
      InterviewQuestion.Difficult difficult = InterviewQuestion.Difficult.LAX;
      if (response.talker() != null && !response.talker().isEmpty()) {
        int talkerType = response.talker().get(0);
        difficult = talkerType == 0
            ? InterviewQuestion.Difficult.STRICT
            : InterviewQuestion.Difficult.LAX;
      }

      childQuestion = InterviewQuestion.builder()
          .interviewId(interview.getInterviewId())
          .parentQuestionId(parentQuestion)  // 부모 질문 설정
          .content(childQuestionText)
          .s3Key(ttsUploadInfo.s3Key())
          .difficult(difficult)
          .build();

      childQuestion = interviewQuestionRepository.save(childQuestion);
      log.info("[InterviewService] 꼬리질문 생성 완료 - questionId={}, difficult={}",
          childQuestion.getInterviewQuestionId(), difficult);
    }

    // Step 10: 응답 DTO 생성
    if (childQuestion == null) {
      log.warn("[InterviewService] 꼬리질문이 생성되지 않음");
      throw ApiException.of(HttpStatus.INTERNAL_SERVER_ERROR, "꼬리질문 생성에 실패했습니다.");
    }

    String childQuestionPresignedUrl = s3Util.generateDownloadPresignedUrl(
        childQuestion.getS3Key());

    log.info("[InterviewService] 꼬리질문 있는 답변 등록 완료 - questionId={}",
        childQuestion.getInterviewQuestionId());

    return new RelatedQuestionResponseDto(
        childQuestion.getInterviewQuestionId(),
        childQuestionPresignedUrl,
        childQuestion.getContent(),
        childQuestion.getDifficult()
    );
  }

  @Override
  public S3UploadInfo generateInterviewAudioUploadUrl(String fileName) {
    log.info("[InterviewService] 인터뷰 오디오 파일 업로드 URL 발급 시도");

    // S3Util을 사용하여 Presigned URL 발급 (Redis 티켓 자동 생성, 15분 유효)
    S3Util.S3UploadInfo uploadInfo = s3Util.generateUploadPresignedUrl(
        "interviews/audio",  // S3 디렉토리 경로
        fileName
    );

    log.info("[InterviewService] 업로드 URL 발급 완료 - s3Key: {}", uploadInfo.s3Key());
    return uploadInfo;
  }

  @Override
  @Transactional
  public void endInterview(Long userId,
      InterviewEndRequestDto interviewEndRequestDto) {
    log.info("[InterviewService] 인터뷰 종료 시도 - interviewId={}, userId={}", 
        interviewEndRequestDto.interviewId(), userId);
    
    // 면접 존재 확인 및 권한 검증
    Interview interview = interviewRepository.findById(interviewEndRequestDto.interviewId())
        .orElseThrow(() -> {
            log.warn("[InterviewService] 인터뷰 종료 실패 - 인터뷰 없음");
            return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
        });
    
    // 권한 확인: 본인의 면접인지 검증
    Resume resume = interview.getResume();
    List<AiResumeQAItem> resumeQA = new ArrayList<>();
    String jdPresignedUrl = "";

    if (resume != null) {
      // 기존 로직: Resume가 있을 때 (일반 면접)
      if (!resume.getUserId().equals(userId)) {
        log.warn("[InterviewService] 인터뷰 종료 실패 - 접근 권한 없음");
        throw ApiException.of(HttpStatus.FORBIDDEN, ErrorMessage.ACCESS_DENIED);
      }
      
      // Resume의 Answer 리스트 조회
      List<Answer> answers = answerRepository.findByResumeId(resume.getResumeId());
      
      // Resume Q&A 배열 구성
      for (int i = 0; i < answers.size(); i++) {
        Answer answer = answers.get(i);
        String num = String.valueOf(i + 1);
        String question = answer.getQuestion().getContent();
        String answerContent = answer.getContent() != null ? answer.getContent() : "";
        
        resumeQA.add(new AiResumeQAItem(
            num,
            question,
            answerContent,
            answerContent
        ));
      }
      
      // JD presigned URL 발급
      JobPosting jobPosting = resume.getJobPosting();
      if (jobPosting != null && jobPosting.getS3PreferenceFileKey() != null 
          && !jobPosting.getS3PreferenceFileKey().isBlank()) {
        jdPresignedUrl = s3Util.generateDownloadPresignedUrl(jobPosting.getS3PreferenceFileKey());
      }
    } else {
      // 시연용 면접: Resume가 null일 때 - 빈 배열 사용 (FastAPI에서 빈 배열 허용)
      log.info("[InterviewService] 시연용 면접 - 빈 자소서 배열 사용");
      resumeQA = new ArrayList<>();
      
      // JD 찾기: Interview 엔티티에 저장된 companyId로 JobPosting 조회
      Long companyId = interview.getCompanyId();
      if (companyId != null) {
        List<JobPosting> jobPostings = jobPostingRepository.findByCompanyCompanyId(companyId);
        Optional<JobPosting> latestJobPosting = jobPostings.stream()
            .filter(jp -> jp.getS3PreferenceFileKey() != null && !jp.getS3PreferenceFileKey().isBlank())
            .max(Comparator.comparing(JobPosting::getCreatedAt));
        
        if (latestJobPosting.isPresent()) {
          jdPresignedUrl = s3Util.generateDownloadPresignedUrl(
              latestJobPosting.get().getS3PreferenceFileKey());
          log.info("[InterviewService] 시연용 면접 - JD 찾기 완료: companyId={}", companyId);
        }
      }
    }
    
    // 상태 확인: IN_PROGRESS 상태일 때만 종료 가능
    if (interview.getProgressStatus() != ProgressStatus.IN_PROGRESS) {
        log.warn("[InterviewService] 인터뷰 종료 실패 - 진행 중이 아닌 상태: {} (interviewId: {})",
            interview.getProgressStatus(), interviewEndRequestDto.interviewId());
        throw ApiException.of(HttpStatus.CONFLICT, ErrorMessage.INTERVIEW_NOT_IN_PROGRESS);
    }
    
    // qna_history 구성 (모든 질문-답변 쌍들)
    List<InterviewQuestion> allQuestions = interviewQuestionRepository
        .findByInterviewId(interview.getInterviewId());
    
    // 질문-답변 매핑 생성 (질문 ID -> 답변)
    Map<Long, String> questionAnswerMap = new HashMap<>();
    for (InterviewQuestion question : allQuestions) {
        Optional<Reply> replyOpt = replyRepository
            .findByInterviewQuestionInterviewQuestionId(question.getInterviewQuestionId());
        if (replyOpt.isPresent()) {
            questionAnswerMap.put(question.getInterviewQuestionId(), replyOpt.get().getContent());
        }
    }
    
    // qna_history 구성 (부모 질문들만 포함)
    List<Map<String, String>> qnaHistory = new ArrayList<>();
    for (InterviewQuestion question : allQuestions) {
        // 부모 질문만 포함 (parentQuestionId가 null인 것들)
        if (question.getParentQuestionId() == null) {
            Map<String, String> qaPair = new HashMap<>();
            qaPair.put("question", question.getContent());
            qaPair.put("answer", questionAnswerMap.getOrDefault(question.getInterviewQuestionId(), ""));
            qnaHistory.add(qaPair);
        }
    }
    
    // Step: S3 키 검증 및 presigned URL 생성
    String s3Key = interviewEndRequestDto.s3Key();
    
    // Redis 티켓 검증 (유효한 업로드 티켓인지 확인)
    s3Util.validateS3Ticket(s3Key);
    log.info("[InterviewService] S3 업로드 티켓 검증 완료 - s3Key: {}", s3Key);
    
    // S3 key로 presigned URL 발급 (FastAPI에 전달할 용도)
    String preflightUrl = s3Util.generateDownloadPresignedUrl(s3Key);
    log.info("[InterviewService] Presigned URL 발급 완료 - s3Key: {}", s3Key);
    
    // FastAPI end 인터뷰 입력 데이터 구성
    AiEndInterviewInput aiInput = AiEndInterviewInput.builder()
        .jd(jdPresignedUrl)
        .resume(resumeQA != null ? resumeQA : new ArrayList<>())
        .qnaHistory(qnaHistory != null ? qnaHistory : new ArrayList<>())
        .preflightUrls(List.of(preflightUrl))
        .build();
    
    log.info("[InterviewService] FastAPI end 요청 준비 완료 - resumeQA 개수: {}, qnaHistory 개수: {}, preflightUrl: {}",
        resumeQA.size(), qnaHistory.size(), s3Key);
    
    // 면접 상태를 COMPLETED로 변경
    interview.complete();
    interviewRepository.save(interview);
    log.info("[InterviewService] 인터뷰 상태 변경 완료 - COMPLETED (interviewId: {})", 
        interview.getInterviewId());
    
    // 워치에 종료 요청 푸시 전송
    try {
      firebasePushService.sendStopRequest(userId, interview.getInterviewId());
      log.info("[InterviewService] 건강데이터 종료 푸시 전송 완료 - interviewId={}, userId={}",
          interview.getInterviewId(), userId);
    } catch (Exception e) {
      log.warn("[InterviewService] 건강데이터 종료 푸시 전송 실패 - interviewId={}, userId={}, err={}",
          interview.getInterviewId(), userId, e.getMessage());
    }

    // 리포트를 CREATING 상태로 생성
    reportService.createReport(interview.getInterviewId());
    log.info("[InterviewService] 리포트 생성 완료 (CREATING 상태) - interviewId: {}", 
        interview.getInterviewId());
    
    // FastAPI end 엔드포인트 호출 (에러 처리 포함)
    try {
      AiEndInterviewResponse response = aiClient.endInterview(aiInput);
      log.info("[InterviewService] FastAPI end 응답 수신 - status: {}", response.getStatus());
      
      // 이벤트 발행 (트랜잭션 커밋 후 리스너가 비동기로 리포트 업데이트)
      eventPublisher.publishEvent(
          new InterviewEndedEvent(interview.getInterviewId(), response)
      );
      
      log.info("[InterviewService] 인터뷰 종료 완료 및 리포트 업데이트 이벤트 발행 - interviewId={}", 
          interview.getInterviewId());
    } catch (Exception e) {
      // FastAPI 호출 실패 시 인터뷰와 리포트를 FAILED 상태로 변경
      // 별도 트랜잭션(REQUIRES_NEW)으로 실행하여 부모 트랜잭션 롤백과 무관하게 저장
      log.error("[InterviewService] FastAPI end 호출 실패 - interviewId: {}, error: {}", 
          interview.getInterviewId(), e.getMessage(), e);
      
      try {
        // 인터뷰 상태를 FAILED로 변경 (별도 트랜잭션)
        interviewStatusService.markFailed(interview.getInterviewId());
        
        // 리포트 상태를 FAILED로 변경 (별도 트랜잭션)
        reportService.markReportAsFailed(interview.getInterviewId());
        
        log.info("[InterviewService] 인터뷰 및 리포트 실패 처리 완료 - interviewId: {}", 
            interview.getInterviewId());
      } catch (Exception statusChangeException) {
        // 상태 변경 실패는 로그만 남기고 원본 예외를 던짐 (상태 변경 실패는 치명적이지 않음)
        log.error("[InterviewService] 상태 변경 실패 처리 중 오류 발생 - interviewId: {}, error: {}", 
            interview.getInterviewId(), statusChangeException.getMessage(), statusChangeException);
      }
      
      // 원본 예외를 다시 던져서 컨트롤러에서 처리할 수 있도록 함
      throw e;
    }
  }

  @Override
  @Transactional
  public DemoInterviewResponseDto createDemoInterview(Long userId, Long jobPostingId) {
    log.info("[InterviewService] 시연용 면접 생성 시도 - userId={}, jobPostingId={}", userId, jobPostingId);
    
    // 1. JobPosting 조회 (존재 여부 확인 및 Company 정보 포함)
    JobPosting jobPosting = jobPostingRepository.findById(jobPostingId)
        .orElseThrow(() -> {
          log.warn("[InterviewService] 시연용 면접 생성 실패 - 채용 공고 없음 (jobPostingId: {})", jobPostingId);
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.JOB_POSTING_NOT_FOUND);
        });
    
    // 2. Company 정보 추출
    Company company = jobPosting.getCompany();
    if (company == null) {
      log.warn("[InterviewService] 시연용 면접 생성 실패 - 채용 공고에 회사 정보 없음 (jobPostingId: {})", jobPostingId);
      throw ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.COMPANY_NOT_FOUND);
    }
    
    // 3. Interview 생성 (Resume = null, companyId 저장)
    Interview interview = Interview.builder()
        .resume(null)  // Resume 없이 생성
        .companyId(company.getCompanyId())  // JobPosting의 Company ID 저장
        .progressStatus(ProgressStatus.NOT_STARTED)
        .relatedQuestion(true)  // 꼬리질문 활성화
        .build();
    Interview savedInterview = interviewRepository.save(interview);
    log.info("[InterviewService] 시연용 Interview 생성 완료 - interviewId={}, jobPostingId={}, companyId={}", 
        savedInterview.getInterviewId(), jobPostingId, company.getCompanyId());
    
    // 4. 고정된 질문 1개만 DB 저장
    InterviewQuestion demoQuestion = InterviewQuestion.builder()
        .interviewId(savedInterview.getInterviewId())
        .content("1분 자기소개 해주세요")
        .s3Key("interviews/demo/intro_question.wav")  // 고정된 S3 key
        .difficult(InterviewQuestion.Difficult.LAX)
        .build();
    InterviewQuestion savedQuestion = interviewQuestionRepository.save(demoQuestion);
    log.info("[InterviewService] 시연용 질문 생성 완료 - questionId={}", savedQuestion.getInterviewQuestionId());
    
    // 5. Presigned URL 생성
    String presignedUrl = s3Util.generateDownloadPresignedUrl(savedQuestion.getS3Key());
    
    // 6. 1분 자기소개용 더미데이터 생성 (프론트에서 보여주기용)
    String exampleIntroduction = getExampleIntroduction();
    
    log.info("[InterviewService] 시연용 면접 생성 완료 - interviewId={}, questionId={}", 
        savedInterview.getInterviewId(), savedQuestion.getInterviewQuestionId());
    
    return new DemoInterviewResponseDto(
        savedInterview.getInterviewId(),
        savedQuestion.getInterviewQuestionId(),
        presignedUrl,
        exampleIntroduction
    );
  }

  /**
   * 1분 자기소개용 더미데이터 반환 (프론트에서 보여주기용)
   * 사용자가 참고할 수 있는 예시 자기소개 텍스트
   */
  private String getExampleIntroduction() {
    return "안녕하세요. 저는 OO대학교 컴퓨터공학과를 졸업한 홍길동입니다. " +
           "주로 Unity를 활용한 VR/AR 개발과 Spring Boot 기반의 백엔드 개발에 관심이 많습니다. " +
           "대학 졸업 프로젝트로 VR 시뮬레이션 게임을 개발한 경험이 있으며, " +
           "팀 프로젝트를 통해 협업 능력과 문제 해결 능력을 기를 수 있었습니다. " +
           "귀사에서 VR/AR 기술을 활용한 혁신적인 서비스를 개발하는 데 기여하고 싶습니다. " +
           "감사합니다.";
  }

}


