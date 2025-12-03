package com.ssafy.s13p21b204.interview.dto;

import jakarta.validation.constraints.NotNull;

public record InterviewRequestDto(

    @NotNull(message = "자소서 ID는 필수입니다.") Long resumeId,
    Boolean relatedQuestion

) {

}
