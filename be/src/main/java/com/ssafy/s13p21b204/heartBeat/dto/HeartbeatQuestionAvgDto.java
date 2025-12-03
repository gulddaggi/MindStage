package com.ssafy.s13p21b204.heartBeat.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import java.time.LocalDateTime;

@Schema(description = "질문 구간별 평균 심박수 응답 DTO")
public record HeartbeatQuestionAvgDto(
    @Schema(description = "질문 ID", example = "101")
    Long questionId,

    @Schema(description = "구간 시작 시각", example = "2025-11-05T14:05:00")
    LocalDateTime startAt,

    @Schema(description = "구간 종료 시각", example = "2025-11-05T14:07:30")
    LocalDateTime endAt,

    @Schema(description = "평균 BPM(정수)", example = "78")
    Integer avgBpm
) {}


