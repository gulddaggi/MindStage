package com.ssafy.s13p21b204.resume.service;

import com.ssafy.s13p21b204.resume.dto.ResumeDetailWrapperDto;
import com.ssafy.s13p21b204.resume.dto.ResumeRequestDto;
import com.ssafy.s13p21b204.resume.dto.ResumeResponseDto;
import java.util.List;

public interface ResumeService {

  void registerResume(Long userId, ResumeRequestDto resumeRequestDto);

  List<ResumeResponseDto> getResumes(Long userId);

  /**
   * 자소서 상세 조회
   */
  ResumeDetailWrapperDto getResume(Long userId, Long resumeId);

  /**
   * 자소서 삭제 (소프트 딜리트)
   */
  void deleteResume(Long userId, Long resumeId);
}
