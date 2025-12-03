  package com.ssafy.s13p21b204.heartBeat.dto;

  import java.time.LocalDateTime;

  public record BpmWithMeasureAtDto(
      Integer bpm,
      LocalDateTime measureAt
  ) {

  }
