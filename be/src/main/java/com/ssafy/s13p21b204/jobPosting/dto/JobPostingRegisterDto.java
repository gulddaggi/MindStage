package com.ssafy.s13p21b204.jobPosting.dto;

import com.ssafy.s13p21b204.jobPosting.entity.JobPosting.Part;
import com.ssafy.s13p21b204.question.dto.QuestionRequestDto;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Future;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import java.time.LocalDateTime;
import java.util.List;

@Schema(
    description = "채용 공고 등록 요청 DTO",
    example = """
        {
          "companyId": 1,
          "part": "SOFTWARE",
          "questions": [
            {
              "question": "현대오토에버의 해당 직무에 지원한 이유와 앞으로 현대오토에버에서 키워 나갈 커리어 계획을 작성해주시기 바랍니다.",
              "limit": 1000
            },
            {
              "question": "귀하의 강점과 약점에 대해서 구체적인 사례를 들어 설명해주세요.",
              "limit": 800
            }
          ],
          "createdAt": "2025-11-03T10:00:00",
          "expiredAt": "2025-12-31T23:59:59",
          "s3PreferenceFileKey": "job-postings/preferences/6822da5e-4bc7-4e0f-8d8c-ffd75ed7558d_job-preferences.pdf"
        }
        """
)
public record JobPostingRegisterDto(
    @Schema(
        description = "기업 ID",
        example = "3",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotNull(message = "기업 ID는 필수입니다.")
    @Positive(message = "기업 ID는 양수여야 합니다.")
    Long companyId,

    @Schema(
        description = "채용 직무 구분",
        example = "SOFTWARE",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotNull(message = "채용 직무는 필수입니다.")
    Part part,

    @Schema(
        description = "면접 질문 리스트 (최소 1개 이상, 질문 2개 예시 참고)",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotEmpty(message = "질문은 최소 1개 이상 필요합니다.")
    @Valid
    List<QuestionRequestDto> questions,

    @Schema(
        description = "채용 공고 생성 일시",
        example = "2025-11-03T10:00:00",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotNull(message = "생성 일시는 필수입니다.")
    LocalDateTime createdAt,

    @Schema(
        description = "채용 공고 마감 일시 (생성 일시보다 이후여야 함)",
        example = "2025-12-31T23:59:59",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotNull(message = "마감 일시는 필수입니다.")
    @Future(message = "마감 일시는 미래 시간이어야 합니다.")
    LocalDateTime expiredAt,

    @Schema(
        description = "S3에 업로드된 직무 우대사항 PDF 파일 Key (선택 사항)",
        example = "job-postings/preferences/abc123-uuid_preferences.pdf"
    )
    String s3PreferenceFileKey
) {

}
