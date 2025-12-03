package com.ssafy.s13p21b204.jobPosting.controller;

import com.ssafy.s13p21b204.global.util.ApiResult;
import com.ssafy.s13p21b204.jobPosting.dto.JobPostingResponseDto;
import com.ssafy.s13p21b204.jobPosting.service.JobPostingService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "채용 공고",description = "채용 공고 관리 API")
@RestController
@RequestMapping("/api/JobPosting")
@RequiredArgsConstructor
public class JobPostingController {

  private final JobPostingService jobPostingService;

  @Operation(
      summary = "채용 공고 목록 반환",
      description = "현재 진행 중인 채용 공고들을 전부 반환합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "채용 공고 목록 조회 성공"
      )
  })
  @GetMapping("/list")
  public ResponseEntity<ApiResult<List<JobPostingResponseDto>>> getAllJobPosting(){
    return ResponseEntity.ok(ApiResult.success(jobPostingService.findAll()));
  }

}
