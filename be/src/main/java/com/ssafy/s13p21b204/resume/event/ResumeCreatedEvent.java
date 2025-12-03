package com.ssafy.s13p21b204.resume.event;

import com.ssafy.s13p21b204.answer.dto.AnswerRequestDto;
import java.util.List;

public record ResumeCreatedEvent(
    Long resumeId,
    Long userId,
    Long jobPostingId,
    List<AnswerRequestDto> answers
) {}

