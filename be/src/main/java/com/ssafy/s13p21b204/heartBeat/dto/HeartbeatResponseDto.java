package com.ssafy.s13p21b204.heartBeat.dto;

import com.ssafy.s13p21b204.heartBeat.entity.Heartbeat;
import io.swagger.v3.oas.annotations.media.Schema;

import java.time.LocalDateTime;

@Schema(description = "심박수 데이터 응답 DTO")
public record HeartbeatResponseDto(
    @Schema(description = "MongoDB Document ID", example = "507f1f77bcf86cd799439011")
    String id,

    @Schema(description = "면접 ID", example = "1")
    Long interviewId,

    @Schema(description = "심박수 (BPM)", example = "72")
    Integer bpm,

    @Schema(description = "측정 시간", example = "2025-11-05T14:00:00")
    LocalDateTime measuredAt,

    @Schema(description = "서버 수신 시간", example = "2025-11-05T14:20:00")
    LocalDateTime receivedAt,

    @Schema(description = "심박수 상태 (NORMAL: 정상, LOW: 서맥, HIGH: 빈맥)", example = "NORMAL")
    String status
) {
  public static HeartbeatResponseDto from(Heartbeat heartbeat) {
    return new HeartbeatResponseDto(
        heartbeat.getHeartBeatId(),
        heartbeat.getInterviewId(),
        heartbeat.getBpm(),
        heartbeat.getMeasuredAt(),
        heartbeat.getReceivedAt(),
        heartbeat.getStatus()
    );
  }
}

