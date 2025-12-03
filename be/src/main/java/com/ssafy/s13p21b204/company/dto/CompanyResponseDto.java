package com.ssafy.s13p21b204.company.dto;

import com.ssafy.s13p21b204.company.entity.Company;
import com.ssafy.s13p21b204.company.entity.Company.Size;
import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "기업 정보 응답 DTO")
public record CompanyResponseDto(
    @Schema(
        description = "기업 ID", 
        example = "1"
    )
    Long companyId,
    
    @Schema(
        description = "기업명", 
        example = "현대오토에버"
    )
    String companyName,
    
    @Schema(
        description = "기업 규모", 
        example = "LARGE"
    )
    Size size
) {
  public static CompanyResponseDto of(Company company) {
    return new CompanyResponseDto(
        company.getCompanyId(),
        company.getName(),
        company.getSize()
    );
  }

}
