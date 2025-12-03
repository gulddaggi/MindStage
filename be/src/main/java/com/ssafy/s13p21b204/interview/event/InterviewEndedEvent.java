package com.ssafy.s13p21b204.interview.event;

import com.ssafy.s13p21b204.global.fastapi.dto.AiEndInterviewResponse;

public record InterviewEndedEvent(
    Long interviewId,
    AiEndInterviewResponse aiEndInterviewResponse
) {}

