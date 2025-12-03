package com.ssafy.s13p21b204.heartBeat.service.impl;

import com.ssafy.s13p21b204.heartBeat.dto.HeartbeatBatchRequestDto;
import com.ssafy.s13p21b204.heartBeat.dto.HeartbeatQuestionAvgDto;
import com.ssafy.s13p21b204.heartBeat.entity.Heartbeat;
import com.ssafy.s13p21b204.heartBeat.repository.HeartbeatRepository;
import com.ssafy.s13p21b204.heartBeat.service.HeartbeatService;
import com.ssafy.s13p21b204.interview.repository.InterviewRepository;
import com.ssafy.s13p21b204.interview.repository.InterviewQuestionRepository;
import com.ssafy.s13p21b204.interview.repository.ReplyRepository;
import com.ssafy.s13p21b204.interview.entity.InterviewQuestion;
import com.ssafy.s13p21b204.interview.entity.Reply;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;

import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.List;
import java.util.OptionalDouble;
import java.util.Map;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
@Slf4j
public class HeartbeatServiceimpl implements HeartbeatService {

  private final HeartbeatRepository heartbeatRepository;
  private final InterviewRepository interviewRepository;
  private final InterviewQuestionRepository interviewQuestionRepository;
  private final ReplyRepository replyRepository;

  /**
   * 면접 종료 후 전체 심박수 데이터 배치 저장 (15-20분치)
   */
  @Override
  public void saveHeartbeatBatch(HeartbeatBatchRequestDto request) {
    log.info("[HeartbeatService] 심박수 데이터 배치 저장 시도 - interviewId: {}", request.interviewId());
    
    // 면접 세션 검증 (시연용 면접도 포함)
    interviewRepository.findById(request.interviewId())
        .orElseThrow(() -> {
          log.warn("[HeartbeatService] 심박수 데이터 배치 저장 실패 - 존재하지 않는 면접 (interviewId: {})", request.interviewId());
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
        });

//    // 본인 면접인지 확인 (Interview에서 Resume을 통해 userId 확인)
//    if (!interview.getResume().getUserId().equals(userId)) {
//      log.warn("[HeartbeatService] 심박수 데이터 배치 저장 실패 - 권한 없음");
//      throw ApiException.of(HttpStatus.FORBIDDEN, ErrorMessage.ACCESS_DENIED);
//    }

    // 배치 데이터를 MongoDB Document로 변환
    List<Heartbeat> heartbeats = request.dataPoints().stream()
        .map(dataPoint -> {
          String status = analyzeHeartbeatStatus(dataPoint.bpm());
          return Heartbeat.builder()
              .interviewId(request.interviewId())
              .bpm(dataPoint.bpm())
              .measuredAt(dataPoint.measuredAt())
              .deviceUuid(request.deviceUuid())
              .status(status)
              .build();
        })
        .collect(Collectors.toList());

    // MongoDB 배치 저장
    heartbeatRepository.saveAll(heartbeats);

    log.info("[HeartbeatService] 심박수 데이터 배치 저장 완료");
  }


  /**
   * 심박수 상태 분석
   */
  private String analyzeHeartbeatStatus(Integer bpm) {
    if (bpm == null) return "UNKNOWN";
    if (bpm < 60) return "LOW";      // 서맥 (Bradycardia)
    if (bpm > 100) return "HIGH";    // 빈맥 (Tachycardia)
    return "NORMAL";
  }



  /**
   * 인터뷰 ID만으로 질문-심박 매핑 (백엔드에서 질문/답변 타이밍 추론)
   * - startAt: InterviewQuestion.createdAt (초기 질문의 생성시점 또는 꼬리질문 생성시점)
   * - endAt: 해당 질문 Reply.createdAt, 없으면 다음(자식) 질문 createdAt, 둘 다 없으면 마지막 심박 시각
   * - startAt이 첫 심박 시각보다 이전이면 첫 심박 시각으로 클램프
   */
  @Override
  public List<HeartbeatQuestionAvgDto> mapBpmByInterview(Long interviewId) {
    log.info("[HeartbeatService] 인터뷰 기반 자동 매핑 시도 - interviewId: {}", interviewId);

    // 면접 세션 검증 (시연용 면접도 포함)
    interviewRepository.findById(interviewId)
        .orElseThrow(() -> {
          log.warn("[HeartbeatService] 인터뷰 기반 자동 매핑 실패 - 존재하지 않는 면접 (interviewId: {})", interviewId);
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.INTERVIEW_NOT_FOUND);
        });

    // 심박 전체 로드(시간순)
    List<Heartbeat> all = heartbeatRepository.findByInterviewIdOrderByMeasuredAtAsc(interviewId);
    LocalDateTime firstHb = all.isEmpty() ? null : all.get(0).getMeasuredAt();
    LocalDateTime lastHb = all.isEmpty() ? null : all.get(all.size() - 1).getMeasuredAt();

    // 인터뷰의 모든 질문 로드
    List<InterviewQuestion> questions = interviewQuestionRepository.findByInterviewId(interviewId);

    // 자식질문(꼬리질문) 인덱스: parent -> children createdAt 오름차순
    Map<Long, List<InterviewQuestion>> childrenByParent = questions.stream()
        .filter(q -> q.getParentQuestionId() != null)
        .collect(Collectors.groupingBy(q -> q.getParentQuestionId().getInterviewQuestionId()));

    // 질문ID -> Reply
    Map<Long, Reply> replyByQuestionId = replyRepository
        .findByInterviewQuestionInterviewQuestionIdIn(
            questions.stream().map(InterviewQuestion::getInterviewQuestionId).collect(Collectors.toList())
        )
        .stream()
        .collect(Collectors.toMap(r -> r.getInterviewQuestion().getInterviewQuestionId(), r -> r));

    List<HeartbeatQuestionAvgDto> result = new ArrayList<>();

    for (InterviewQuestion q : questions) {
      LocalDateTime computedStart = q.getCreatedAt();
      if (firstHb != null && (computedStart == null || computedStart.isBefore(firstHb))) {
        computedStart = firstHb; // 초기 질문 생성이 너무 이른 경우, 첫 심박 시각으로 클램프
      }

      // 우선순위로 endAt 결정: Reply.createdAt > 첫 자식(createdAt) > 마지막 심박 시각
      LocalDateTime computedEnd = null;
      Reply r = replyByQuestionId.get(q.getInterviewQuestionId());
      if (r != null && r.getCreatedAt() != null) {
        computedEnd = r.getCreatedAt();
      } else {
        List<InterviewQuestion> children = childrenByParent.get(q.getInterviewQuestionId());
        if (children != null && !children.isEmpty()) {
          LocalDateTime minChildCreated = children.stream()
              .map(InterviewQuestion::getCreatedAt)
              .filter(dt -> dt != null)
              .min(LocalDateTime::compareTo)
              .orElse(null);
          computedEnd = minChildCreated;
        } else {
          computedEnd = lastHb; // 데이터가 없으면 마지막 심박 시각으로 근사
        }
      }

      final LocalDateTime start = computedStart;
      final LocalDateTime end = computedEnd;

      if (start == null || end == null || end.isBefore(start)) {
        result.add(new HeartbeatQuestionAvgDto(
            q.getInterviewQuestionId(), start, end, null
        ));
        continue;
      }

      List<Integer> bpms = all.stream()
          .filter(h -> !h.getMeasuredAt().isBefore(start) && !h.getMeasuredAt().isAfter(end))
          .map(Heartbeat::getBpm)
          .collect(Collectors.toList());

      Integer avg = null;
      if (!bpms.isEmpty()) {
        OptionalDouble avgOpt = bpms.stream().mapToInt(Integer::intValue).average();
        if (avgOpt.isPresent()) {
          avg = (int) Math.round(avgOpt.getAsDouble());
        }
      }

      result.add(new HeartbeatQuestionAvgDto(
          q.getInterviewQuestionId(),
          start,
          end,
          avg
      ));
    }

    log.info("[HeartbeatService] 인터뷰 기반 자동 매핑 완료 - {}개 항목", result.size());
    return result;
  }
}
