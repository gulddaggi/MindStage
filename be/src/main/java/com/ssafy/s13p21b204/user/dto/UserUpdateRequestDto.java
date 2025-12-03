package com.ssafy.s13p21b204.user.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

@Schema(description = "사용자 정보 수정 요청 DTO")
public record UserUpdateRequestDto(
        @Schema(description = "변경할 사용자 이름", example = "홍길동")
                @NotBlank(message = "이름을 입력해주세요.")
                @Size(min = 2, max = 10, message = "이름은 2자 이상 10자 이하로 입력해주세요.")
                @Pattern(regexp = "^[가-힣a-zA-Z]+$", message = "이름은 한글 또는 영문만 입력 가능합니다.")
                String username) {}
