package com.ssafy.s13p21b204.watch.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;

@Schema(description = "갤럭시 워치 등록 요청 DTO")
public record GalaxyRequestDto(
    @Schema(description = "워치 고유 식별자(UUID)", example = "a1b2c3d4-e5f6-7890-abcd-ef1234567890", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotBlank(message = "워치 UUID는 필수입니다.")
    String uuid,
    
    @Schema(description = "워치 모델명", example = "Galaxy Watch 6", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotBlank(message = "워치 모델명은 필수입니다.")
    String modelName
) {

}
