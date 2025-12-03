package com.ssafy.s13p21b204.auth.service;


import com.ssafy.s13p21b204.auth.dto.ChangePasswordDto;
import com.ssafy.s13p21b204.auth.dto.LoginRequestDto;
import com.ssafy.s13p21b204.auth.dto.SignUpRequestDto;
import com.ssafy.s13p21b204.auth.dto.TokenResponseDto;
import com.ssafy.s13p21b204.user.dto.UserResponseDto;

public interface AuthService {

  /**
   * 회원가입
   */
  UserResponseDto signUp(SignUpRequestDto signUpRequestDto);

  /**
   * 로그인
   */
  TokenResponseDto login(LoginRequestDto loginRequest);

  /**
   * 토큰 재발급
   */
  TokenResponseDto refreshToken(String refreshToken);

  /**
   * 로그아웃 - Refresh Token DB에서 삭제
   */
  void logout(Long userId);

  /**
   * 이메일 사용 가능 여부 확인
   */
  boolean isEmailAvailable(String email);

  /**
   * 비밀번호 변경
   */
  void changePassword(Long userId, ChangePasswordDto changePasswordDto);
}
