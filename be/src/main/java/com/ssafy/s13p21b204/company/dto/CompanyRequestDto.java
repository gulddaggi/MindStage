package com.ssafy.s13p21b204.company.dto;

import com.ssafy.s13p21b204.company.entity.Company.Size;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

@Schema(description = "기업 등록 요청 DTO")
public record CompanyRequestDto(
    @Schema(
        description = "기업명", 
        example = "현대오토에버",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotBlank(message = "기업명은 필수입니다.")
    @jakarta.validation.constraints.Size(min = 1, max = 100, message = "기업명은 1자 이상 100자 이하로 입력해주세요.")
    String companyName,
    
    @Schema(
        description = "기업 규모", 
        example = "MID_SIZED",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotNull(message = "기업 규모는 필수입니다.")
    Size size
) {

}
