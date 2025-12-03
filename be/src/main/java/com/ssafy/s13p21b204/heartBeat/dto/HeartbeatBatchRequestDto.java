package com.ssafy.s13p21b204.heartBeat.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.Valid;
import jakarta.validation.constraints.*;

import java.time.LocalDateTime;
import java.util.List;

@Schema(
    description = "면접 종료 후 심박수 데이터 배치 저장 요청 DTO (면접 15-20분 전체 데이터)",
    example = """
        {
          "interviewId": 1,
          "deviceUuid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "dataPoints": [
            {
              "bpm": 72,
              "measuredAt": "2025-11-05T14:00:00"
            },
            {
              "bpm": 74,
              "measuredAt": "2025-11-05T14:00:01"
            }
          ]
        }
        """
)
public record HeartbeatBatchRequestDto(
    @Schema(
        description = "면접 ID",
        example = "1",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotNull(message = "면접 ID는 필수입니다.")
    @Positive(message = "면접 ID는 양수여야 합니다.")
    Long interviewId,

    @Schema(
        description = "갤럭시 워치 고유 식별자(UUID)",
        example = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotBlank(message = "디바이스 UUID는 필수입니다.")
    String deviceUuid,

    @Schema(
        description = "심박수 데이터 포인트 목록 (면접 15-20분 전체)",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotEmpty(message = "심박수 데이터는 최소 1개 이상이어야 합니다.")
    @Valid
    List<HeartbeatDataPoint> dataPoints
) {
  @Schema(description = "개별 심박수 데이터 포인트")
  public record HeartbeatDataPoint(
      @Schema(
          description = "심박수 (BPM)",
          example = "72",
          requiredMode = Schema.RequiredMode.REQUIRED
      )
      @NotNull(message = "심박수는 필수입니다.")
      @Min(value = 30, message = "심박수는 30 이상이어야 합니다.")
      @Max(value = 250, message = "심박수는 250 이하여야 합니다.")
      Integer bpm,

      @Schema(
          description = "측정 시간",
          example = "2025-11-05T14:00:00",
          requiredMode = Schema.RequiredMode.REQUIRED
      )
      @NotNull(message = "측정 시간은 필수입니다.")
      LocalDateTime measuredAt
  ) {}
}