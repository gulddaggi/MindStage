package com.ssafy.s13p21b204.heartBeat.entity;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Document;
import org.springframework.data.mongodb.core.index.Indexed;
import org.springframework.data.mongodb.core.index.CompoundIndex;
import org.springframework.data.mongodb.core.index.CompoundIndexes;

import java.time.LocalDateTime;

@Document(collection = "heartbeats")
@CompoundIndexes({
    @CompoundIndex(name = "interview_measured_idx", def = "{'interviewId': 1, 'measuredAt': 1}"),
    @CompoundIndex(name = "user_measured_idx", def = "{'userId': 1, 'measuredAt': -1}")
})
@Getter
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class Heartbeat {

  @Id
  private String heartBeatId;

  // 면접 세션 ID (MySQL Interview의 PK)
  @Indexed
  private Long interviewId;

  // 갤럭시 워치 UUID
  private String deviceUuid;

  // 심박수 (BPM - Beats Per Minute)
  private Integer bpm;

  // 갤럭시 워치에서 측정한 시간
  @Indexed
  private LocalDateTime measuredAt;

  // 서버가 데이터를 수신한 시간 (자동 설정)
  @CreatedDate
  private LocalDateTime receivedAt;

  // 심박수 상태 분석 결과
  private String status;  // "NORMAL", "LOW", "HIGH"
}