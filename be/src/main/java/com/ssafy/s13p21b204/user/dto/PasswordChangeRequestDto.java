package com.ssafy.s13p21b204.user.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

@Schema(description = "비밀번호 변경 요청 DTO")
public record PasswordChangeRequestDto(
        @Schema(description = "현재 비밀번호", example = "CurrentPass123!")
                @NotBlank(message = "현재 비밀번호를 입력해주세요.")
                String oldPassword,
        @Schema(description = "새 비밀번호", example = "NewPassword123!")
                @NotBlank(message = "새 비밀번호를 입력해주세요.")
                @Size(min = 8, max = 20, message = "비밀번호는 8자 이상 20자 이하로 입력해주세요.")
                @Pattern(
                        regexp =
                                "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[@$!%*?&])[A-Za-z\\d@$!%*?&]{8,}$",
                        message = "비밀번호는 영문 대/소문자, 숫자, 특수문자를 각각 하나 이상 포함해야 합니다.")
                String newPassword) {}
