package com.ssafy.s13p21b204.notification.dto;

import jakarta.validation.constraints.NotBlank;

public record TokenCommitByUuidRequestDto(
    @NotBlank(message = "uuid는 필수입니다.")
    String uuid,

    @NotBlank(message = "token은 필수입니다.")
    String token
) {}


