package com.ssafy.s13p21b204.jobPosting.service;

import com.ssafy.s13p21b204.global.util.S3Util.S3UploadInfo;
import com.ssafy.s13p21b204.jobPosting.dto.JobPostingRegisterDto;
import com.ssafy.s13p21b204.jobPosting.dto.JobPostingResponseDto;
import java.util.List;

public interface JobPostingService {

  /**
   * 채용공고 등록
   */
  void createJobPosting(JobPostingRegisterDto jobPostingRegisterDto);

  /**
   * 직무 우대사항 PDF 파일 업로드용 Presigned URL 발급
   *
   * @param fileName 원본 파일명
   * @return S3 업로드 정보 (presignedUrl, s3Key, expirationSeconds)
   */
  S3UploadInfo generatePreferenceFileUploadUrl(String fileName);

  /**
   * 채용공고 반환 로직
   */
  List<JobPostingResponseDto> findAll();

}

