package com.ssafy.s13p21b204.auth.dto;

import com.ssafy.s13p21b204.user.entity.User;
import com.ssafy.s13p21b204.user.entity.User.Role;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

@Schema(description = "회원가입 요청 DTO")
public record SignUpRequestDto(
    @Schema(description = "이메일", example = "user@example.com")
    @NotBlank(message = "이메일은 필수 입력 값입니다.")
    @Email(message = "올바른 이메일 형식이 아닙니다.")
    String email,

    @Schema(description = "비밀번호", example = "Password123!")
    @NotBlank(message = "비밀번호는 필수 입력 값입니다.")
    @Size(min = 8, max = 20, message = "비밀번호는 8자 이상 20자 이하로 입력해주세요.")
    @Pattern(
        regexp = "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[@$!%*?&])[A-Za-z\\d@$!%*?&]{8,}$",
        message = "비밀번호는 영문 대/소문자, 숫자, 특수문자를 각각 하나 이상 포함해야 합니다."
    )
    String password,

    @Schema(description = "사용자 이름", example = "홍길동")
    @NotBlank(message = "이름은 필수 입력 값입니다.")
    @Size(min = 2, max = 10, message = "이름은 2자 이상 10자 이하로 입력해주세요.")
    @Pattern(
        regexp = "^[가-힣a-zA-Z]+$",
        message = "이름은 한글 또는 영문만 입력 가능합니다."
    )
    String name
) {

  public static User of(SignUpRequestDto signUpRequestDto) {

    return User.builder()
        .email(signUpRequestDto.email)
        .password(signUpRequestDto.password)
        .name(signUpRequestDto.name)
        .role(Role.VISITOR)
        .build();
  }
}
