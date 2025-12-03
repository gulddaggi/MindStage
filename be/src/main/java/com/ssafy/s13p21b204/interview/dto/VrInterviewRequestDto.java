package com.ssafy.s13p21b204.interview.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;

@Schema(description = "VR 면접 시작 요청 DTO")
public record VrInterviewRequestDto(
    @Schema(description = "자소서 ID", example = "1", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "자소서 ID는 필수입니다.")
    @Positive(message = "자소서 ID는 양수여야 합니다.")
    Long resumeId,
    
    @Schema(description = "워치 사용 여부", example = "true", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "워치 사용 여부는 필수입니다.")
    Boolean WatchEnabled
) {

}
