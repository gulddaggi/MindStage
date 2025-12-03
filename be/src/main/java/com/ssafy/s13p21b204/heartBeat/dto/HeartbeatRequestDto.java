package com.ssafy.s13p21b204.heartBeat.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.*;

import java.time.LocalDateTime;

@Schema(description = "심박수 데이터 저장 요청 DTO")
public record HeartbeatRequestDto(
    @Schema(description = "면접 ID", example = "1", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "면접 ID는 필수입니다")
    Long interviewId,

    @Schema(description = "심박수 (BPM)", example = "72", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "심박수는 필수입니다")
    @Min(value = 30, message = "심박수는 30 이상이어야 합니다")
    @Max(value = 250, message = "심박수는 250 이하여야 합니다")
    Integer bpm,

    @Schema(description = "측정 시간", example = "2025-11-05T14:30:00", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "측정 시간은 필수입니다")
    LocalDateTime measuredAt,

    @Schema(description = "디바이스 UUID", example = "a1b2c3d4-e5f6-7890-abcd-ef1234567890", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotBlank(message = "디바이스 UUID는 필수입니다")
    String deviceUuid
) {
}