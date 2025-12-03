package com.ssafy.s13p21b204.company.controller;

import com.ssafy.s13p21b204.company.dto.CompanyRequestDto;
import com.ssafy.s13p21b204.company.dto.CompanyResponseDto;
import com.ssafy.s13p21b204.company.service.CompanyService;
import com.ssafy.s13p21b204.global.util.ApiResult;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "(관리자) 기업", description = "기업 정보 관리 API")
@RestController
@RequiredArgsConstructor
@RequestMapping("/api/admin/company")
@PreAuthorize("hasRole('ADMIN')")
public class CompanyAdminController {

  private final CompanyService companyService;

  @Operation(
      summary = "(관리자용) 기업 등록",
      description = "서비스에서 제공할 기업을 생성합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "기업 등록 성공"
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 회사명은 필수입니다.
              • 회사 규모는 필수입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"회사명은 필수입니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "409",
          description = """
              • 이미 회사가 등록되어있습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"이미 회사가 등록되어있습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/register")
  public ApiResult<Void> registerCompany(@Valid @RequestBody CompanyRequestDto companyRequestDto) {
    companyService.registerCompany(companyRequestDto);
    return ApiResult.create(null);
  }

  @Operation(
      summary = "(관리자용) 기업 삭제",
      description = "서비스에서 제공해주지 않을 기업을 삭제합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "기업 삭제 성공"
      ),
      @ApiResponse(
          responseCode = "404",
          description = """
              • 기업이 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"기업이 없습니다.\"}"
              )
          )
      )
  })
  @DeleteMapping("/delete/{companyId}")
  public ApiResult<Void> deleteCompany(@PathVariable Long companyId) {
    companyService.deleteCompany(companyId);
    return ApiResult.success(null);
  }

  @Operation(
      summary = "(관리자용) 기업 목록 조회",
      description = "서비스에서 제공하는 면접 기업들을 반환합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "기업 목록 조회 성공"
      )
  })
  @GetMapping("/list")
  public ApiResult<List<CompanyResponseDto>> getCompanies() {
    return ApiResult.success(companyService.getCompanies());
  }


}
