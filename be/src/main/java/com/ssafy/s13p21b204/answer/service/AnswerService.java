package com.ssafy.s13p21b204.answer.service;

import com.ssafy.s13p21b204.answer.dto.AnswerRequestDto;
import java.util.List;

public interface AnswerService {
  void registerAll(Long resumeId,List<AnswerRequestDto> answerRequestDtos);

}
