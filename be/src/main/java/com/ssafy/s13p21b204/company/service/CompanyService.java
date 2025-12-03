package com.ssafy.s13p21b204.company.service;

import com.ssafy.s13p21b204.company.dto.CompanyRequestDto;
import com.ssafy.s13p21b204.company.dto.CompanyResponseDto;
import java.util.List;

public interface CompanyService {

  /**
   * 회사 등록(서비스 지원)
   */
  void registerCompany(CompanyRequestDto companyRequestDto);

  /**
   * 회사 삭제(서비스 종료)
   */
  void deleteCompany(Long companyId);

  /**
   * 회사 목록 반환
   */
  List<CompanyResponseDto> getCompanies();

}
