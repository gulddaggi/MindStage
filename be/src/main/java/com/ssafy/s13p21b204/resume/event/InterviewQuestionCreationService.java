
package com.ssafy.s13p21b204.resume.event;

import com.ssafy.s13p21b204.answer.entity.Answer;
import com.ssafy.s13p21b204.answer.repository.AnswerRepository;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.fastapi.AiClient;
import com.ssafy.s13p21b204.global.fastapi.dto.AiInterviewInput;
import com.ssafy.s13p21b204.global.fastapi.dto.AiInterviewResponse;
import com.ssafy.s13p21b204.global.fastapi.dto.AiResumeQAItem;
import com.ssafy.s13p21b204.global.util.S3Util;
import com.ssafy.s13p21b204.interview.entity.InterviewQuestion;
import com.ssafy.s13p21b204.interview.repository.InterviewQuestionRepository;
import com.ssafy.s13p21b204.jobPosting.entity.JobPosting;
import com.ssafy.s13p21b204.resume.entity.Resume;
import com.ssafy.s13p21b204.resume.repository.ResumeRepository;
import java.util.ArrayList;
import java.util.List;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * FastAPI를 호출해 면접 질문을 생성하는 서비스.
 */
@Service
@RequiredArgsConstructor
@Slf4j
public class InterviewQuestionCreationService {

  private static final int EXPECTED_QUESTION_COUNT = 5;

  private final ResumeRepository resumeRepository;
  private final AnswerRepository answerRepository;
  private final InterviewQuestionRepository interviewQuestionRepository;
  private final S3Util s3Util;
  private final AiClient aiClient;
  private final InterviewQuestionValidationService validationService;

  @Transactional
  public void createQuestions(Long interviewId, ResumeCreatedEvent event) {
    log.info("[InterviewQuestionCreationService] FastAPI 기반 질문 생성 시작 ");

    Resume resume = resumeRepository.findByIdWithJobPostingAndCompany(event.resumeId())
        .orElseThrow(() -> {
          log.warn("[InterviewQuestionCreationService] 질문 생성 실패 - Resume 미존재 ");
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.RESUME_NOT_FOUND);
        });

    JobPosting jobPosting = resume.getJobPosting();
    List<Answer> answers = answerRepository.findByResumeId(event.resumeId());

    List<S3Util.S3UploadInfo> uploadInfos = new ArrayList<>();
    List<String> presignedUrls = new ArrayList<>();
    for (int i = 1; i <= EXPECTED_QUESTION_COUNT; i++) {
      String fileName = String.format("interview_%d_question_%d.wav", interviewId, i);
      S3Util.S3UploadInfo uploadInfo = s3Util.generateUploadPresignedUrl("interviews/questions",
          fileName);
      uploadInfos.add(uploadInfo);
      presignedUrls.add(uploadInfo.presignedUrl());
      log.info("[InterviewQuestionCreationService] 질문 {} Presigned URL 발급 - key={}", i,
          uploadInfo.s3Key());
    }

    AiInterviewInput aiInput = buildAiInterviewInput(jobPosting, answers, presignedUrls);
    AiInterviewResponse response = aiClient.startInterview(aiInput);
    int actualQuestionCount = response.textFromTts() != null ? response.textFromTts().size() : 0;
    log.info("[InterviewQuestionCreationService] FastAPI 응답 수신 - 질문 수={}", actualQuestionCount);

    if (actualQuestionCount != EXPECTED_QUESTION_COUNT) {
      throw ApiException.of(HttpStatus.UNPROCESSABLE_ENTITY,
          ErrorMessage.INTERVIEW_QUESTION_COUNT_MISMATCH);
    }

    List<InterviewQuestion> questions = new ArrayList<>();
    for (int i = 0; i < actualQuestionCount; i++) {
      String questionText = response.textFromTts().get(i);
      String s3Key = uploadInfos.get(i).s3Key();
      InterviewQuestion.Difficult difficult = InterviewQuestion.Difficult.LAX;
      if (response.talker() != null && i < response.talker().size()) {
        difficult = response.talker().get(i) == 0
            ? InterviewQuestion.Difficult.STRICT
            : InterviewQuestion.Difficult.LAX;
      }
      InterviewQuestion question = InterviewQuestion.builder()
          .interviewId(interviewId)
          .content(questionText)
          .s3Key(s3Key)
          .difficult(difficult)
          .build();
      questions.add(question);
    }

    List<InterviewQuestion> saved = interviewQuestionRepository.saveAll(questions);
    validationService.validate(interviewId, saved);
    log.info("[InterviewQuestionCreationService] 질문 생성 및 검증 완료 - interviewId={}, 총 {}개",
        interviewId, saved.size());
  }

  private AiInterviewInput buildAiInterviewInput(JobPosting jobPosting, List<Answer> answers,
      List<String> presignedUrls) {
    String jd = s3Util.generateDownloadPresignedUrl(jobPosting.getS3PreferenceFileKey());
    List<AiResumeQAItem> resumeQA = new ArrayList<>();
    for (int i = 0; i < answers.size(); i++) {
      Answer answer = answers.get(i);
      String num = String.valueOf(i + 1);
      String question = answer.getQuestion().getContent();
      String answerContent = answer.getContent() != null ? answer.getContent() : "";
      resumeQA.add(new AiResumeQAItem(num, question, answerContent, answerContent));
    }
    return new AiInterviewInput(
        jd != null ? jd : "",
        resumeQA,
        new ArrayList<>(),
        "",
        presignedUrls
    );
  }
}