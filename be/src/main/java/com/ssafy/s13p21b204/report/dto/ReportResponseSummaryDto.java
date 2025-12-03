package com.ssafy.s13p21b204.report.dto;

import com.ssafy.s13p21b204.jobPosting.entity.JobPosting.Part;
import io.swagger.v3.oas.annotations.media.Schema;
import java.time.LocalDateTime;

@Schema(description = "레포트 요약 응답 DTO")
public record ReportResponseSummaryDto(
    @Schema(description = "레포트 ID", example = "507f1f77bcf86cd799439011")
    String reportId,
    
    @Schema(description = "회사명", example = "삼성전자")
    String CompanyName,
    
    @Schema(description = "직무", example = "SOFTWARE", allowableValues = {"MARKETING", "SOFTWARE", "QUALITY"})
    Part part,
    
    @Schema(description = "레포트 생성일시", example = "2025-01-15T10:30:00")
    LocalDateTime createdAt
) {

}
