package com.ssafy.s13p21b204.heartBeat.service;

import com.ssafy.s13p21b204.heartBeat.dto.HeartbeatBatchRequestDto;
import com.ssafy.s13p21b204.heartBeat.dto.HeartbeatQuestionAvgDto;

import java.util.List;

public interface HeartbeatService {

  /**
   * 면접 종료 후 전체 심박수 데이터 배치 저장 (15-20분치)
   */
  void saveHeartbeatBatch(HeartbeatBatchRequestDto request);

  /**
   * 인터뷰 ID만으로 질문-심박 매핑 (백엔드에서 질문/답변 타이밍 추론)
   */
  List<HeartbeatQuestionAvgDto> mapBpmByInterview(Long interviewId);

}
