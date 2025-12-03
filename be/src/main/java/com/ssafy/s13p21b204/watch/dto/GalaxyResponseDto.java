package com.ssafy.s13p21b204.watch.dto;

import com.ssafy.s13p21b204.watch.entity.GalaxyWatch;
import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "갤럭시 워치 응답 DTO")
public record GalaxyResponseDto(
    @Schema(description = "워치 ID", example = "1")
    Long galaxyWatchId,
    
    @Schema(description = "워치 모델명", example = "Galaxy Watch 6")
    String modelName
) {
  public static GalaxyResponseDto from(GalaxyWatch galaxyWatch) {
    return new GalaxyResponseDto(
        galaxyWatch.getGalaxyWatchId(),
        galaxyWatch.getModelName()
    );
  }

}
