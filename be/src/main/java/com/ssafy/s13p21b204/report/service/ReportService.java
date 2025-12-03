package com.ssafy.s13p21b204.report.service;

import com.ssafy.s13p21b204.global.fastapi.dto.AiEndInterviewResponse;
import com.ssafy.s13p21b204.heartBeat.dto.HeartbeatQuestionAvgDto;
import com.ssafy.s13p21b204.report.dto.ReportResponseDetailDto;
import com.ssafy.s13p21b204.report.dto.ReportResponseSummaryDto;
import java.util.List;

public interface ReportService {

  // CREATING 상태로 리포트 생성
  void createReport(Long interviewId);

  // FastAPI 응답을 받아 리포트 업데이트 (COMPLETED 상태로 변경)
  void updateReport(Long interviewId, AiEndInterviewResponse aiEndInterviewResponse);

  // 리포트를 FAILED 상태로 변경 (FastAPI 호출 실패 시 사용)
  void markReportAsFailed(Long interviewId);

  // 기존 메서드 (하위 호환성을 위해 유지, 내부적으로 createReport + updateReport 호출)
  void saveReport(Long interviewId, AiEndInterviewResponse aiEndInterviewResponse);

  ReportResponseDetailDto getReport(Long userId,String reportId);

  List<ReportResponseSummaryDto> getReports(Long userId);

  /**
   * 리포트 삭제 (소프트 딜리트)
   * @param userId 사용자 ID
   * @param reportId 리포트 ID
   */
  void deleteReport(Long userId, String reportId);

  /**
   * reportId만으로 질문별 평균 심박 데이터를 조회한다.
   * 내부에서 reportId -> interviewId를 해석해 HeartbeatService에 위임한다.
   */
  List<HeartbeatQuestionAvgDto> getQuestionHeartbeatAvg(Long userId, String reportId);
}
