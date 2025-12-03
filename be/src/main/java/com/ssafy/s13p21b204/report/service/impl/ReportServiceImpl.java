package com.ssafy.s13p21b204.report.service.impl;

import com.ssafy.s13p21b204.global.entity.ProgressStatus;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.fastapi.dto.AiEndInterviewResponse;
import com.ssafy.s13p21b204.heartBeat.dto.BpmWithMeasureAtDto;
import com.ssafy.s13p21b204.heartBeat.dto.HeartbeatQuestionAvgDto;
import com.ssafy.s13p21b204.heartBeat.entity.Heartbeat;
import com.ssafy.s13p21b204.heartBeat.repository.HeartbeatRepository;
import com.ssafy.s13p21b204.heartBeat.service.HeartbeatService;
import com.ssafy.s13p21b204.interview.entity.Interview;
import com.ssafy.s13p21b204.interview.entity.InterviewQuestion;
import com.ssafy.s13p21b204.interview.entity.Reply;
import com.ssafy.s13p21b204.interview.repository.InterviewQuestionRepository;
import com.ssafy.s13p21b204.interview.repository.InterviewRepository;
import com.ssafy.s13p21b204.interview.repository.ReplyRepository;
import com.ssafy.s13p21b204.company.repository.CompanyRepository;
import com.ssafy.s13p21b204.jobPosting.entity.JobPosting.Part;
import com.ssafy.s13p21b204.report.dto.ReportResponseDetailDto;
import com.ssafy.s13p21b204.report.dto.ReportResponseSummaryDto;
import com.ssafy.s13p21b204.report.entity.QnaItem;
import com.ssafy.s13p21b204.report.entity.Report;
import com.ssafy.s13p21b204.report.repository.ReportRepository;
import com.ssafy.s13p21b204.report.service.ReportService;
import com.ssafy.s13p21b204.resume.entity.Resume;
import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;
import java.util.regex.Pattern;

@Service
@RequiredArgsConstructor
@Slf4j
public class ReportServiceImpl implements ReportService {

  private final ReportRepository reportRepository;
  private final InterviewRepository interviewRepository;
  private final HeartbeatRepository heartbeatRepository;
  private final HeartbeatService heartbeatService;
  private final InterviewQuestionRepository interviewQuestionRepository;
  private final ReplyRepository replyRepository;
  private final CompanyRepository companyRepository;

  @Override
  @Transactional
  public void createReport(Long interviewId) {
    log.info("[ReportService] 리포트 생성 시도 (CREATING 상태) - interviewId={}", interviewId);

    // 이미 리포트가 존재하는지 확인 (여러 개가 있을 수 있으므로 가장 최신 것 확인)
    List<Report> existingReports = reportRepository.findAllByInterviewId(interviewId);
    Optional<Report> latestReportOpt = findLatestReport(existingReports);
    if (latestReportOpt.isPresent()) {
      Report latestReport = latestReportOpt.get();
      log.warn("[ReportService] 리포트가 이미 존재함 - interviewId={}, reportId={}, 리포트 수={}",
          interviewId, latestReport.getReportId(), existingReports.size());
      return;
    }

    // Q&A 리스트 수집 (부모 질문만 포함, 생성 순서대로 정렬)
    List<QnaItem> qnaList = collectQnaList(interviewId);
    log.info("[ReportService] Q&A 리스트 수집 완료 - interviewId: {}, Q&A 개수: {}", interviewId,
        qnaList.size());

    // CREATING 상태로 리포트 생성
    // MongoDB unique index가 있지만 동시성 문제로 중복 생성될 수 있으므로 try-catch로 처리
    try {
      Report report = Report.builder()
          .interviewId(interviewId)
          .progressStatus(ProgressStatus.CREATING)
          .scores(new HashMap<>())
          .report("")
          .qnaList(qnaList)
          .build();

      reportRepository.save(report);
      log.info("[ReportService] 리포트 생성 완료 (CREATING 상태) - reportId={}, interviewId={}, Q&A 개수: {}",
          report.getReportId(), interviewId, qnaList.size());
    } catch (org.springframework.dao.DuplicateKeyException e) {
      // MongoDB unique index 위반 시 (동시 요청으로 인한 중복 생성)
      log.warn("[ReportService] 리포트 생성 중복 시도 감지 - interviewId={}, 이미 다른 요청에서 생성됨", interviewId);
      // 이미 생성된 리포트를 조회하여 반환 (정상 동작)
      List<Report> reportsAfterSave = reportRepository.findAllByInterviewId(interviewId);
      Optional<Report> savedReportOpt = findLatestReport(reportsAfterSave);
      if (savedReportOpt.isPresent()) {
        log.info("[ReportService] 중복 생성 시도 후 기존 리포트 확인 완료 - reportId={}", 
            savedReportOpt.get().getReportId());
      }
    } catch (Exception e) {
      log.error("[ReportService] 리포트 생성 실패 - interviewId={}, error={}", interviewId, e.getMessage(), e);
      throw e;
    }
  }

  @Override
  @Transactional
  public void updateReport(Long interviewId, AiEndInterviewResponse aiEndInterviewResponse) {
    log.info("[ReportService] 리포트 업데이트 시도 - interviewId={}", interviewId);

    // 기존 리포트 조회 (여러 개가 있을 수 있으므로 가장 최신 것 선택)
    List<Report> reports = reportRepository.findAllByInterviewId(interviewId);
    Report report = findLatestReport(reports)
        .orElseThrow(() -> {
          log.error("[ReportService] 리포트 업데이트 실패 - 리포트 없음 (interviewId: {})", interviewId);
          return new RuntimeException("리포트를 찾을 수 없습니다. interviewId: " + interviewId);
        });
    
    if (reports.size() > 1) {
      log.warn("[ReportService] 여러 리포트 발견 - interviewId={}, 리포트 수={}, 가장 최신 리포트 사용 (reportId={})",
          interviewId, reports.size(), report.getReportId());
    }

    // scores 추출
    Map<String, Integer> scores = aiEndInterviewResponse.getScores();
    if (scores == null) {
      log.warn("[ReportService] FastAPI 응답 scores가 null입니다.");
      scores = new HashMap<>();
    }

    // qnaList 가져오기 (각 항목에 label을 추가할 예정)
    List<QnaItem> qnaList = report.getQnaList() != null
        ? new ArrayList<>(report.getQnaList())
        : new ArrayList<>();
    int qnaCount = qnaList.size();
    log.info("[ReportService] qnaList 개수: {}", qnaCount);

    // labels 추출
    List<Integer> labelsFromApi = aiEndInterviewResponse.getLabels();

    if (labelsFromApi == null || labelsFromApi.isEmpty()) {
      log.warn("[ReportService] FastAPI 응답 labels가 null이거나 비어있습니다. qnaList의 각 항목에 기본값(0)을 설정합니다.");
      // qnaList의 각 항목에 기본값(0: 중립) 설정 (새로운 리스트로 재구성)
      qnaList = qnaList.stream()
          .map(item -> QnaItem.builder()
              .question(item.getQuestion())
              .relatedQuestion(item.getRelatedQuestion())
              .answer(item.getAnswer())
              .labels(List.of(0))
              .build())
          .collect(Collectors.toList());
    } else {
      // 문장 분할 규칙을 AI와 동일하게 적용하여 각 답변의 문장 수를 계산한 뒤,
      // 평면 labels 리스트를 답변별 문장 라벨 리스트로 슬라이싱하여 매핑한다.
      int labelsCount = labelsFromApi.size();
      log.info("[ReportService] FastAPI 응답 labels 개수(문장 단위): {}", labelsCount);

      List<Integer> sentenceCounts = new ArrayList<>(qnaCount);
      int totalNeeded = 0;
      for (QnaItem item : qnaList) {
        int cnt = countSentences(item.getAnswer());
        sentenceCounts.add(cnt);
        totalNeeded += cnt;
      }

      if (totalNeeded == 0) {
        log.warn("[ReportService] 모든 answer가 비어있거나 문장 분할 결과가 0개입니다. 각 항목에 기본값(0)을 설정합니다.");
        qnaList = qnaList.stream()
            .map(item -> QnaItem.builder()
                .question(item.getQuestion())
                .relatedQuestion(item.getRelatedQuestion())
                .answer(item.getAnswer())
                .labels(List.of(0))
                .build())
            .collect(Collectors.toList());
      } else {
        if (labelsCount < totalNeeded) {
          log.warn("[ReportService] labels 개수({})가 필요 문장 수({})보다 작습니다. 부족분은 기본값(0)으로 패딩합니다.",
              labelsCount, totalNeeded);
        } else if (labelsCount > totalNeeded) {
          log.warn("[ReportService] labels 개수({})가 필요 문장 수({})보다 큽니다. 초과 라벨은 무시됩니다.", labelsCount,
              totalNeeded);
        }

        int pos = 0;
        List<QnaItem> updatedQnaList = new ArrayList<>();
        for (int i = 0; i < qnaCount; i++) {
          QnaItem qnaItem = qnaList.get(i);
          int need = Math.max(0, sentenceCounts.get(i));

          List<Integer> labelSlice = new ArrayList<>();
          if (need > 0) {
            int end = Math.min(pos + need, labelsCount);
            if (pos < end) {
              labelSlice.addAll(labelsFromApi.subList(pos, end));
            }
            // 부족하면 0으로 패딩
            while (labelSlice.size() < need) {
              labelSlice.add(0);
            }
            pos = Math.min(pos + need, labelsCount);
          } else {
            // 문장 수가 0인 경우: 기본값 0 하나를 부여(기존 UI/로직과의 일관성 유지)
            labelSlice = List.of(0);
          }

          QnaItem updatedItem = QnaItem.builder()
              .question(qnaItem.getQuestion())
              .relatedQuestion(qnaItem.getRelatedQuestion())
              .answer(qnaItem.getAnswer())
              .labels(labelSlice)
              .build();
          updatedQnaList.add(updatedItem);
        }
        qnaList = updatedQnaList;

        if (pos < labelsCount) {
          log.warn("[ReportService] labels가 {}개 남았습니다(사용되지 않음). pos={}, total={}",
              (labelsCount - pos), pos, labelsCount);
        }
      }
    }

    log.info("[ReportService] qnaList에 label 매핑 완료 - 총 {}개 항목 (각 answer별 문장 라벨 리스트)",
        qnaList.size());

    // report 추출
    String reportText = aiEndInterviewResponse.getReport();
    if (reportText == null) {
      log.warn("[ReportService] FastAPI 응답 report가 null입니다.");
      reportText = "";
    }

    // 리포트 업데이트 (COMPLETED 상태로 변경)
    Report updatedReport = Report.builder()
        .reportId(report.getReportId())
        .interviewId(interviewId)
        .progressStatus(ProgressStatus.COMPLETED)
        .scores(scores != null ? new HashMap<>(scores) : new HashMap<>())
        .report(reportText)
        .qnaList(qnaList) // label이 포함된 qnaList 사용
        .status(report.getStatus())
        .createdAt(report.getCreatedAt())
        .deletedAt(report.getDeletedAt())
        .build();

    reportRepository.save(updatedReport);
    log.info("[ReportService] 리포트 업데이트 완료 (COMPLETED 상태) - reportId={}, interviewId={}",
        updatedReport.getReportId(), interviewId);

    // 인터뷰 상태를 REPORTED로 변경 (JPA 트랜잭션 필요)
    Interview interview = interviewRepository.findById(interviewId)
        .orElseThrow(() -> {
          log.error("[ReportService] 인터뷰 조회 실패 - interviewId={}", interviewId);
          return new RuntimeException("인터뷰를 찾을 수 없습니다. interviewId: " + interviewId);
        });

    interview.markAsReported();
    interviewRepository.save(interview);
    log.info("[ReportService] 인터뷰 상태 변경 완료 (REPORTED) - interviewId={}", interviewId);
  }

  @Override
  @Transactional(propagation = Propagation.REQUIRES_NEW)
  public void markReportAsFailed(Long interviewId) {
    log.info("[ReportService] 리포트 실패 처리 시도 - interviewId={}", interviewId);

    // 기존 리포트 조회 (여러 개가 있을 수 있으므로 가장 최신 것 선택)
    List<Report> reports = reportRepository.findAllByInterviewId(interviewId);
    Optional<Report> reportOpt = findLatestReport(reports);
    if (reportOpt.isEmpty()) {
      log.warn("[ReportService] 리포트가 존재하지 않아 실패 처리 불가 - interviewId={}", interviewId);
      return;
    }

    Report report = reportOpt.get();
    
    if (reports.size() > 1) {
      log.warn("[ReportService] 여러 리포트 발견 - interviewId={}, 리포트 수={}, 가장 최신 리포트 사용 (reportId={})",
          interviewId, reports.size(), report.getReportId());
    }

    // 리포트를 FAILED 상태로 변경
    Report failedReport = Report.builder()
        .reportId(report.getReportId())
        .interviewId(interviewId)
        .progressStatus(ProgressStatus.FAILED)
        .scores(report.getScores() != null ? new HashMap<>(report.getScores()) : new HashMap<>())
        .report(report.getReport() != null ? report.getReport() : "")
        .qnaList(
            report.getQnaList() != null ? new ArrayList<>(report.getQnaList()) : new ArrayList<>())
        .status(report.getStatus())
        .createdAt(report.getCreatedAt())
        .deletedAt(report.getDeletedAt())
        .build();

    reportRepository.save(failedReport);
    log.info("[ReportService] 리포트 실패 처리 완료 (FAILED 상태) - reportId={}, interviewId={}",
        failedReport.getReportId(), interviewId);
  }

  @Override
  public void saveReport(Long interviewId, AiEndInterviewResponse aiEndInterviewResponse) {
    // 하위 호환성을 위한 메서드 (내부적으로 createReport + updateReport 호출)
    log.info("[ReportService] saveReport 호출 (하위 호환성) - interviewId={}", interviewId);
    createReport(interviewId);
    updateReport(interviewId, aiEndInterviewResponse);
  }

  /**
   * 면접 Q&A 리스트 수집 (부모 질문 + 꼬리질문 포함, 생성 순서대로 정렬) 일반 질문: question 필드에 질문 내용 저장 꼬리질문: relatedQuestion
   * 필드에 질문 내용 저장, question은 null
   *
   * @param interviewId 면접 ID
   * @return Q&A 리스트 (QnaItem 리스트)
   */
  private List<QnaItem> collectQnaList(Long interviewId) {
    // 모든 질문 조회 (부모 질문 + 꼬리질문)
    List<InterviewQuestion> allQuestions = interviewQuestionRepository.findByInterviewId(
        interviewId);

    // 부모 질문만 필터링 (parentQuestionId가 null인 것들)
    List<InterviewQuestion> parentQuestions = allQuestions.stream()
        .filter(question -> question.getParentQuestionId() == null)
        .sorted(Comparator.comparing(InterviewQuestion::getCreatedAt)) // 생성 순서대로 정렬
        .collect(Collectors.toList());

    // 꼬리질문만 필터링 (parentQuestionId가 null이 아닌 것들)
    List<InterviewQuestion> childQuestions = allQuestions.stream()
        .filter(question -> question.getParentQuestionId() != null)
        .sorted(Comparator.comparing(InterviewQuestion::getCreatedAt)) // 생성 순서대로 정렬
        .collect(Collectors.toList());

    // 부모 질문 ID -> 꼬리질문 리스트 매핑 생성
    Map<Long, List<InterviewQuestion>> parentToChildrenMap = new HashMap<>();
    for (InterviewQuestion childQuestion : childQuestions) {
      Long parentId = childQuestion.getParentQuestionId().getInterviewQuestionId();
      parentToChildrenMap.computeIfAbsent(parentId, k -> new ArrayList<>()).add(childQuestion);
    }

    // 모든 질문 ID 수집 (부모 + 꼬리질문)
    List<Long> allQuestionIds = allQuestions.stream()
        .map(InterviewQuestion::getInterviewQuestionId)
        .collect(Collectors.toList());

    // 한 번의 쿼리로 모든 답변 조회 (N+1 쿼리 문제 방지)
    List<Reply> replies = replyRepository.findByInterviewQuestionInterviewQuestionIdIn(
        allQuestionIds);

    // 질문 ID -> 답변 매핑 생성
    Map<Long, String> answerMap = replies.stream()
        .collect(Collectors.toMap(
            reply -> reply.getInterviewQuestion().getInterviewQuestionId(),
            Reply::getContent,
            (existing, replacement) -> existing // 중복 시 기존 값 유지
        ));

    // Q&A 리스트 구성 (label은 나중에 추가되므로 빈 리스트로 설정)
    List<QnaItem> qnaList = new ArrayList<>();

    // 부모 질문 먼저 추가
    for (InterviewQuestion parentQuestion : parentQuestions) {
      // 부모 질문 추가 (question 필드에 질문 내용 저장, relatedQuestion은 null)
      QnaItem parentQnaItem = QnaItem.builder()
          .question(parentQuestion.getContent())
          .relatedQuestion(null)
          .answer(answerMap.getOrDefault(parentQuestion.getInterviewQuestionId(), ""))
          .labels(new ArrayList<>()) // label은 FastAPI 응답 후 추가됨
          .build();
      qnaList.add(parentQnaItem);

      // 해당 부모 질문의 꼬리질문들 추가 (부모 질문 바로 아래에 순서대로)
      List<InterviewQuestion> children = parentToChildrenMap.get(
          parentQuestion.getInterviewQuestionId());
      if (children != null && !children.isEmpty()) {
        // 꼬리질문들을 생성 순서대로 정렬
        children.sort(Comparator.comparing(InterviewQuestion::getCreatedAt));

        for (InterviewQuestion childQuestion : children) {
          // 꼬리질문 추가 (relatedQuestion 필드에 질문 내용 저장, question은 null)
          QnaItem childQnaItem = QnaItem.builder()
              .question(null)
              .relatedQuestion(childQuestion.getContent())
              .answer(answerMap.getOrDefault(childQuestion.getInterviewQuestionId(), ""))
              .labels(new ArrayList<>()) // label은 FastAPI 응답 후 추가됨
              .build();
          qnaList.add(childQnaItem);
        }
      }
    }

    return qnaList;
  }

  @Override
  @Transactional(readOnly = true)
  public ReportResponseDetailDto getReport(Long userId, String reportId) {
    log.info("[ReportService] 보고서 조회 시도 - reportId={}, userId={}", reportId, userId);

    // Step 1: Report 조회
    Report report = reportRepository.findById(reportId).orElseThrow(() -> {
      log.warn("[ReportService] 보고서 조회 실패 - 보고서 없음 (reportId: {})", reportId);
      return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.REPORT_NOT_FOUND);
    });

    // Step 2: Interview 조회 및 권한 확인 (시연용 면접도 포함)
    Interview interview = interviewRepository.findById(report.getInterviewId())
        .orElseThrow(() -> {
          log.warn("[ReportService] 인터뷰 조회 실패 - interviewId={}", report.getInterviewId());
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
        });

    Resume resume = interview.getResume();
    if (resume != null) {
      // 일반 면접: userId 확인
      if (!resume.getUserId().equals(userId)) {
        log.warn("[ReportService] 보고서 조회 실패 - 접근 권한 없음 ");
        throw ApiException.of(HttpStatus.FORBIDDEN, ErrorMessage.ACCESS_DENIED);
      }
    } else {
      // 시연용 면접: 권한 검증 생략 (모든 사용자가 조회 가능)
      log.info("[ReportService] 시연용 면접 - 권한 검증 생략");
    }

    // Step 3: Report 상태 확인 (COMPLETED 또는 REPORTED 상태일 때만 조회 가능)
    if (!report.getProgressStatus().equals(ProgressStatus.COMPLETED)
        && !report.getProgressStatus().equals(ProgressStatus.REPORTED)) {
      log.warn("[ReportService] 보고서 조회 실패 - 아직 생성 중 (상태: {})", report.getProgressStatus());
      throw ApiException.of(HttpStatus.CONFLICT, ErrorMessage.REPORT_NOT_READY);
    }

    // Step 4: Heartbeat 데이터 변환 (Heartbeat 리스트에서 bpm 추출)
    // 심박수 측정을 하지 않은 경우 빈 리스트로 반환 (정상 동작)
    List<Heartbeat> heartbeatList = heartbeatRepository
        .findByInterviewIdOrderByMeasuredAtAsc(report.getInterviewId());
    List<BpmWithMeasureAtDto> heartbeats = heartbeatList.stream()
        .filter(h -> h.getBpm() != null)
        .map(h -> new BpmWithMeasureAtDto(h.getBpm(), h.getMeasuredAt()
        ))
        .collect(Collectors.toList());

    if (heartbeats.isEmpty()) {
      log.info("[ReportService] 심박수 데이터 없음 - 측정하지 않았거나 데이터가 없습니다. (interviewId: {})",
          report.getInterviewId());
    } else {
      log.info("[ReportService] 심박수 데이터 조회 완료 - 개수: {}", heartbeats.size());
    }

    // Step 5: qnaList 조회 (각 QnaItem에 labels 리스트가 포함되어 있음)
    // 각 answer에 대응하는 labels 리스트가 QnaItem에 매핑되어 있음
    List<QnaItem> qnaList = report.getQnaList() != null
        ? new ArrayList<>(report.getQnaList())
        : new ArrayList<>();

    log.info(
        "[ReportService] qnaList 개수: {} (각 QnaItem에 question 또는 relatedQuestion, answer, labels 리스트 포함)",
        qnaList.size());

    // Step 6: Average Scores 계산 (같은 jobPostingId를 가진 모든 인터뷰의 REPORTED 상태인 리포트들의 scores 평균)
    HashMap<String, Integer> averageScores = new HashMap<>();
    if (resume != null && resume.getJobPosting() != null) {
      Long jobPostingId = resume.getJobPosting().getJobPostingId();

      // 같은 jobPostingId를 가진 모든 인터뷰 조회 (쿼리 최적화)
      List<Interview> sameJobPostingInterviews = interviewRepository.findByJobPostingId(
          jobPostingId);
      log.info("[ReportService] 같은 채용공고 인터뷰 조회 완료 - jobPostingId: {}, 인터뷰 수: {}",
          jobPostingId, sameJobPostingInterviews.size());

      if (!sameJobPostingInterviews.isEmpty()) {
        // 모든 인터뷰 ID를 리스트로 추출
        List<Long> interviewIds = sameJobPostingInterviews.stream()
            .map(Interview::getInterviewId)
            .collect(Collectors.toList());

        // 한 번의 쿼리로 모든 리포트 조회 (N+1 쿼리 문제 해결)
        List<Report> allReports = reportRepository.findByInterviewIdIn(interviewIds);
        log.info("[ReportService] 리포트 일괄 조회 완료 - 인터뷰 수: {}, 리포트 수: {}",
            interviewIds.size(), allReports.size());

        // COMPLETED 또는 REPORTED 상태인 리포트만 필터링
        List<Report> reports = allReports.stream()
            .filter(r -> r.getProgressStatus().equals(ProgressStatus.COMPLETED)
                || r.getProgressStatus().equals(ProgressStatus.REPORTED))
            .collect(Collectors.toList());

        // scores 평균 계산
        if (!reports.isEmpty()) {
          Map<String, List<Integer>> scoresByCategory = new HashMap<>();

          // 각 리포트의 scores를 카테고리별로 수집
          for (Report r : reports) {
            for (Map.Entry<String, Integer> entry : r.getScores().entrySet()) {
              scoresByCategory.computeIfAbsent(entry.getKey(), k -> new ArrayList<>())
                  .add(entry.getValue());
            }
          }

          // 각 카테고리의 평균 계산
          for (Map.Entry<String, List<Integer>> entry : scoresByCategory.entrySet()) {
            List<Integer> scores = entry.getValue();
            int sum = scores.stream().mapToInt(Integer::intValue).sum();
            int average = sum / scores.size();
            averageScores.put(entry.getKey(), average);
          }
        }

        log.info("[ReportService] 평균 점수 계산 완료 - 리포트 개수: {}, 평균 점수 항목 수: {}",
            reports.size(), averageScores.size());
      }
    }

    log.info("[ReportService] 보고서 조회 완료 - reportId={}", reportId);
    return new ReportResponseDetailDto(
        report.getReport(),
        heartbeats,
        report.getScores(),
        averageScores,
        qnaList
    );
  }

  @Override
  @Transactional(readOnly = true)
  public List<ReportResponseSummaryDto> getReports(Long userId) {
    log.info("[ReportService] 보고서 목록 조회 시도 - userId={}", userId);

    // Step 1: userId로 해당 사용자의 모든 Interview 조회 (일반 면접만, Resume이 있는 것만)
    List<Interview> userInterviews = interviewRepository.findByUserId(userId);
    log.info("[ReportService] 사용자 일반 인터뷰 조회 완료 - userId: {}, 인터뷰 수: {}", userId, userInterviews.size());

    // Step 2: 모든 인터뷰 ID를 리스트로 추출 (일반 면접)
    List<Long> userInterviewIds = userInterviews.stream()
        .map(Interview::getInterviewId)
        .collect(Collectors.toList());
    
    // Step 3: MongoDB에서 COMPLETED 또는 REPORTED 상태인 모든 리포트 조회
    // (시연용 면접도 포함하기 위해 먼저 리포트를 조회)
    List<Report> allReports = reportRepository.findAll().stream()
        .filter(report -> report.getProgressStatus().equals(ProgressStatus.COMPLETED)
            || report.getProgressStatus().equals(ProgressStatus.REPORTED))
        .collect(Collectors.toList());
    
    log.info("[ReportService] 모든 리포트 조회 완료 - 리포트 수: {}", allReports.size());
    
    // Step 4: 리포트의 interviewId 목록 추출
    List<Long> reportInterviewIds = allReports.stream()
        .map(Report::getInterviewId)
        .distinct()
        .collect(Collectors.toList());
    
    // Step 5: 리포트의 interviewId로 인터뷰 일괄 조회 (N+1 쿼리 방지)
    List<Interview> reportInterviews = interviewRepository.findAllById(reportInterviewIds);
    
    // Step 6: 시연용 면접(resume이 null)의 interviewId 목록 추출
    List<Long> demoInterviewIds = reportInterviews.stream()
        .filter(interview -> interview.getResume() == null)
        .map(Interview::getInterviewId)
        .collect(Collectors.toList());
    
    log.info("[ReportService] 시연용 면접 ID 목록 - demoInterviewIds: {}", demoInterviewIds);
    
    // Step 7: 사용자 소유 인터뷰의 리포트만 필터링 (일반 면접 + 시연용 면접)
    List<Report> userReports = allReports.stream()
        .filter(report -> {
          Long interviewId = report.getInterviewId();
          // 일반 면접인 경우 userId 확인
          if (userInterviewIds.contains(interviewId)) {
            return true;
          }
          // 시연용 면접인 경우도 포함
          return demoInterviewIds.contains(interviewId);
        })
        .collect(Collectors.toList());
    
    log.info("[ReportService] 사용자 리포트 필터링 완료 - 리포트 수: {}", userReports.size());
    
    if (userReports.isEmpty()) {
      log.info("[ReportService] 보고서 목록 조회 완료 - 리포트가 없어 빈 리스트 반환");
      return new ArrayList<>();
    }

    // Step 8: 리포트의 interviewId로 Interview 조회 및 DTO 변환
    // Step 5에서 이미 조회한 reportInterviews를 Map으로 변환하여 재사용
    Map<Long, Interview> interviewMap = reportInterviews.stream()
        .collect(Collectors.toMap(Interview::getInterviewId, interview -> interview));
    
    List<ReportResponseSummaryDto> reportSummaries = new ArrayList<>();
    for (Report report : userReports) {
      Long interviewId = report.getInterviewId();
      
      // Step 5에서 이미 조회한 인터뷰 사용
      Interview interview = interviewMap.get(interviewId);
      if (interview == null) {
        log.warn("[ReportService] 인터뷰 조회 실패 - interviewId: {}", interviewId);
        continue;
      }
      Resume resume = interview.getResume();
      
      String companyName;
      Part part;
      LocalDateTime createdAt;
      
      if (resume != null && resume.getJobPosting() != null) {
        // 일반 면접: Resume -> JobPosting -> Company
        com.ssafy.s13p21b204.jobPosting.entity.JobPosting jobPosting = resume.getJobPosting();
        if (jobPosting.getCompany() == null) {
          log.warn("[ReportService] 보고서 목록 조회 실패 - Company가 null (interviewId: {})", interviewId);
          continue;
        }
        companyName = jobPosting.getCompany().getName();
        part = jobPosting.getPart();
        createdAt = report.getCreatedAt() != null ? report.getCreatedAt() : interview.getCreatedAt();
      } else {
        // 시연용 면접: companyId로 Company 조회
        Long companyId = interview.getCompanyId();
        if (companyId == null) {
          log.warn("[ReportService] 보고서 목록 조회 실패 - companyId가 null (interviewId: {})", interviewId);
          continue;
        }
        com.ssafy.s13p21b204.company.entity.Company company = companyRepository.findById(companyId)
            .orElse(null);
        if (company == null) {
          log.warn("[ReportService] 보고서 목록 조회 실패 - Company가 없음 (companyId: {}, interviewId: {})", 
              companyId, interviewId);
          continue;
        }
        companyName = company.getName();
        // 시연용 면접은 part 정보가 없으므로 기본값 사용
        part = Part.SOFTWARE;
        createdAt = report.getCreatedAt() != null ? report.getCreatedAt() : interview.getCreatedAt();
      }

      // ReportResponseSummaryDto 생성
      ReportResponseSummaryDto summaryDto = new ReportResponseSummaryDto(
          report.getReportId(),
          companyName,
          part,
          createdAt
      );

      reportSummaries.add(summaryDto);
    }

    // Step 9: 최신순 정렬 (createdAt 기준 내림차순)
    reportSummaries.sort((a, b) -> {
      LocalDateTime aCreatedAt = a.createdAt();
      LocalDateTime bCreatedAt = b.createdAt();

      if (aCreatedAt == null && bCreatedAt == null) {
        return 0;
      }
      if (aCreatedAt == null) {
        return 1; // null은 뒤로
      }
      if (bCreatedAt == null) {
        return -1; // null은 뒤로
      }
      return bCreatedAt.compareTo(aCreatedAt); // 내림차순
    });

    log.info("[ReportService] 보고서 목록 조회 완료 - userId: {}, 리포트 수: {}", userId,
        reportSummaries.size());
    return reportSummaries;
  }

  @Override
  @Transactional
  public void deleteReport(Long userId, String reportId) {
    log.info("[ReportService] 보고서 삭제 시도 - reportId={}, userId={}", reportId, userId);

    // Step 1: Report 조회 (삭제된 리포트도 포함하여 조회)
    // 삭제된 리포트를 다시 삭제하려는 경우를 명확히 구분하기 위해 findByIdIncludingDeleted 사용
    Report report = reportRepository.findByIdIncludingDeleted(reportId).orElseThrow(() -> {
      log.warn("[ReportService] 보고서 삭제 실패 - 보고서 없음 (reportId: {})", reportId);
      return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.REPORT_NOT_FOUND);
    });

    // Step 2: 이미 삭제된 경우 확인
    if (report.isDeleted()) {
      log.warn("[ReportService] 보고서 삭제 실패 - 이미 삭제됨 (reportId: {})", reportId);
      throw ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.REPORT_NOT_FOUND);
    }

    // Step 3: Interview 조회 및 권한 확인 (시연용 면접도 포함)
    Interview interview = interviewRepository.findById(report.getInterviewId())
        .orElseThrow(() -> {
          log.warn("[ReportService] 인터뷰 조회 실패 - interviewId={}", report.getInterviewId());
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
        });

    Resume resume = interview.getResume();
    if (resume != null) {
      // 일반 면접: userId 확인
      if (!resume.getUserId().equals(userId)) {
        log.warn("[ReportService] 보고서 삭제 실패 - 접근 권한 없음 (reportId: {}, userId: {})", reportId, userId);
        throw ApiException.of(HttpStatus.FORBIDDEN, ErrorMessage.ACCESS_DENIED);
      }
    } else {
      // 시연용 면접: 권한 검증 생략 (모든 사용자가 삭제 가능)
      log.info("[ReportService] 시연용 면접 - 권한 검증 생략");
    }

    // Step 4: 소프트 딜리트 수행
    report.markDeleted();
    reportRepository.save(report);

    log.info("[ReportService] 보고서 삭제 완료 - reportId={}, interviewId={}",
        reportId, report.getInterviewId());
  }

  @Override
  @Transactional(readOnly = true)
  public List<HeartbeatQuestionAvgDto> getQuestionHeartbeatAvg(Long userId, String reportId) {
    log.info("[ReportService] 질문별 평균 심박 조회 시도 - reportId={}, userId={}", reportId, userId);

    // Report 조회
    Report report = reportRepository.findById(reportId).orElseThrow(() -> {
      log.warn("[ReportService] 질문별 평균 심박 조회 실패 - 보고서 없음 (reportId: {})", reportId);
      return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.REPORT_NOT_FOUND);
    });

    // Interview 조회 및 권한 확인 (시연용 면접도 포함)
    Long interviewId = report.getInterviewId();
    log.info("[ReportService] 인터뷰 조회 시도 - interviewId: {}", interviewId);
    
    Optional<Interview> interviewOpt = interviewRepository.findById(interviewId);
    if (interviewOpt.isEmpty()) {
      log.warn("[ReportService] 질문별 평균 심박 조회 실패 - 인터뷰 없음 (interviewId: {})", interviewId);
      // 디버깅: findAll로 확인
      List<Interview> allInterviews = interviewRepository.findAll();
      log.warn("[ReportService] 디버깅 - 전체 인터뷰 수: {}, interviewId 목록: {}", 
          allInterviews.size(), 
          allInterviews.stream().map(Interview::getInterviewId).collect(Collectors.toList()));
      throw ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
    }
    
    Interview interview = interviewOpt.get();
    log.info("[ReportService] 인터뷰 조회 성공 - interviewId: {}, resume: {}", 
        interviewId, interview.getResume() != null ? "있음" : "null(시연용)");

    Resume resume = interview.getResume();
    if (resume != null) {
      // 일반 면접: userId 확인
      if (!resume.getUserId().equals(userId)) {
        log.warn("[ReportService] 질문별 평균 심박 조회 실패 - 접근 권한 없음");
        throw ApiException.of(HttpStatus.FORBIDDEN, ErrorMessage.ACCESS_DENIED);
      }
    } else {
      // 시연용 면접: 권한 검증 생략 (모든 사용자가 조회 가능)
      log.info("[ReportService] 시연용 면접 - 권한 검증 생략");
    }

    // 위임 실행
    List<HeartbeatQuestionAvgDto> result = heartbeatService.mapBpmByInterview(
        report.getInterviewId());
    log.info("[ReportService] 질문별 평균 심박 조회 완료 - count={}", result != null ? result.size() : 0);
    return result;
  }

  /**
   * 여러 리포트 중 가장 최신 리포트를 선택 (createdAt 기준 내림차순)
   * @param reports 리포트 리스트
   * @return 가장 최신 리포트 (Optional)
   */
  private Optional<Report> findLatestReport(List<Report> reports) {
    if (reports == null || reports.isEmpty()) {
      return Optional.empty();
    }
    
    return reports.stream()
        .sorted((a, b) -> {
          LocalDateTime aCreatedAt = a.getCreatedAt();
          LocalDateTime bCreatedAt = b.getCreatedAt();
          if (aCreatedAt == null && bCreatedAt == null) return 0;
          if (aCreatedAt == null) return 1;
          if (bCreatedAt == null) return -1;
          return bCreatedAt.compareTo(aCreatedAt);
        })
        .findFirst();
  }

  /**
   * AI의 문장 분할 규칙과 동일한 정규식을 사용하여 문장을 분할한다. Python:
   * r'(?<=[\.!?])\s+|(?<=다\.)\s+|(?<=요\.)\s+|(?<=,)\s+'
   */
  private static final Pattern SENTENCE_SPLIT_PATTERN =
      Pattern.compile("(?<=[\\.!?])\\s+|(?<=다\\.)\\s+|(?<=요\\.)\\s+|(?<=,)\\s+");

  private static int countSentences(String paragraph) {
    if (paragraph == null) {
      return 0;
    }
    String trimmed = paragraph.trim();
    if (trimmed.isEmpty()) {
      return 0;
    }

    // 줄 단위로 나눈 뒤 각 라인에 대해 문장 분할
    String[] rawLines = trimmed.split("\\R");
    int count = 0;
    for (String rawLine : rawLines) {
      if (rawLine == null) {
        continue;
      }
      String line = rawLine.trim();
      if (line.isEmpty()) {
        continue;
      }

      String[] pieces = SENTENCE_SPLIT_PATTERN.split(line);
      for (String p : pieces) {
        if (p != null && !p.trim().isEmpty()) {
          count++;
        }
      }
    }
    return count;
  }
}
