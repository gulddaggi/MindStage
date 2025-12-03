package com.ssafy.s13p21b204.company.service.impl;

import com.ssafy.s13p21b204.company.dto.CompanyRequestDto;
import com.ssafy.s13p21b204.company.dto.CompanyResponseDto;
import com.ssafy.s13p21b204.company.entity.Company;
import com.ssafy.s13p21b204.company.repository.CompanyRepository;
import com.ssafy.s13p21b204.company.service.CompanyService;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import java.util.ArrayList;
import java.util.List;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@Slf4j
@RequiredArgsConstructor
public class CompanyServiceImpl implements CompanyService {

  private final CompanyRepository companyRepository;

  @Override
  @Transactional
  public void registerCompany(CompanyRequestDto companyRequestDto) {
    log.info("[CompanyService] 기업 등록 시도");
    if (companyRepository.existsByName(companyRequestDto.companyName())) {
      log.warn("[CompanyService] 기업 등록 실패 - 중복된 기업");
      throw ApiException.of(HttpStatus.CONFLICT, ErrorMessage.COMPANY_NAME_ALREADY_EXISTS);
    }
    Company c = Company.builder()
        .name(companyRequestDto.companyName())
        .size(companyRequestDto.size())
        .build();
    companyRepository.save(c);
    log.info("[CompanyService] 기업 등록 완료");
  }

  @Override
  @Transactional
  public void deleteCompany(Long companyId) {
    log.info("[CompanyService] 기업 삭제 시도");
    Company c = companyRepository.findById(companyId).orElseThrow(() -> {
      log.warn("[CompanyService] 기업 삭제 실패 - 존재하지 않는 기업 (ID: {})", companyId);
      return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.COMPANY_NOT_FOUND);
    });
    companyRepository.delete(c);
    log.info("[CompanyService] 기업 삭제 완료");
  }

  @Override
  @Transactional(readOnly = true)
  public List<CompanyResponseDto> getCompanies() {
    log.info("[CompanyService] 전체 기업 목록 조회 시도");
    List<Company> companies = companyRepository.findAll();
    log.info("[CompanyService] 전체 기업 목록 조회 - {}건 조회", companies.size());
    List<CompanyResponseDto> companyResponseDtos = new ArrayList<>();
    for (Company c : companies) {
      companyResponseDtos.add(CompanyResponseDto.of(c));
    }
    log.info("[CompanyService] 전체 기업 목록 조회 성공");
    return companyResponseDtos;
  }


}
