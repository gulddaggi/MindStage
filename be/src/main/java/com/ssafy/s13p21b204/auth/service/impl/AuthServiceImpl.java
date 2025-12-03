package com.ssafy.s13p21b204.auth.service.impl;


import com.ssafy.s13p21b204.auth.dto.ChangePasswordDto;
import com.ssafy.s13p21b204.auth.dto.LoginRequestDto;
import com.ssafy.s13p21b204.auth.dto.SignUpRequestDto;
import com.ssafy.s13p21b204.auth.dto.TokenResponseDto;
import com.ssafy.s13p21b204.auth.service.AuthService;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.redis.RedisDao;
import com.ssafy.s13p21b204.security.service.JwtTokenProvider;
import com.ssafy.s13p21b204.notification.service.FirebasePushService;
import com.ssafy.s13p21b204.user.dto.UserResponseDto;
import com.ssafy.s13p21b204.user.entity.User;
import com.ssafy.s13p21b204.user.entity.User.Role;
import com.ssafy.s13p21b204.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.HttpStatus;
import org.springframework.security.authentication.AuthenticationCredentialsNotFoundException;
import org.springframework.security.authentication.AuthenticationManager;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Slf4j
public class AuthServiceImpl implements AuthService {

  private final UserRepository userRepository;
  private final JwtTokenProvider jwtTokenProvider;
  private final PasswordEncoder passwordEncoder;
  private final AuthenticationManager authenticationManager;
  private final RedisDao redisDao;
  private final FirebasePushService firebasePushService;

  @Value("${jwt.refresh-token-validity-in-seconds:604800}")
  private long refreshTokenValidityInSeconds;

  @Override
  @Transactional
  public UserResponseDto signUp(SignUpRequestDto signUpRequestDto) {
    log.info("[AuthService] 회원가입 시도");

    String normalizedEmail = signUpRequestDto.email().trim();
    if (userRepository.existsByEmail(normalizedEmail)) {
      log.warn("[AuthService] 회원가입 실패 - 이메일 중복");
      throw ApiException.of(HttpStatus.CONFLICT, ErrorMessage.EMAIL_ALREADY_EXISTS);
    }

    User user = SignUpRequestDto.of(signUpRequestDto);

    user.changeEmail(normalizedEmail);

    user.changePassword(passwordEncoder.encode(signUpRequestDto.password()));
    user.changeRole(Role.GENERAL);

    User save = userRepository.save(user);
    log.info("[AuthService] 회원가입 완료, userId={}", save.getUserId());
    return UserResponseDto.from(save);
  }

  @Override
  @Transactional
  public TokenResponseDto login(LoginRequestDto loginRequest) {
    log.info("[AuthService] 로그인 시도");
    try {
      String normalizedEmail = loginRequest.email().trim();
      authenticationManager.authenticate(
          new UsernamePasswordAuthenticationToken(normalizedEmail, loginRequest.password())
      );

      User user = userRepository.findByEmail(normalizedEmail)
          .orElseThrow(() -> {
            log.warn("[AuthService] 로그인 실패 - 사용자 정보 없음");
            return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.USER_NOT_FOUND);
          });

      String accessToken = jwtTokenProvider.createAccessToken(user.getUserId(), user.getEmail(),
          user.getRole().name());

      String refreshToken = jwtTokenProvider.createRefreshToken(user.getUserId(), user.getEmail(),
          user.getRole().name());

      // Redis에 리프레시 토큰 저장 (userId를 키로 사용)
      String redisKey = "RT:" + user.getUserId();
      redisDao.setRefreshToken(redisKey, refreshToken, refreshTokenValidityInSeconds);

      log.info("[AuthService] 로그인 성공 userId={}", user.getUserId());

      return new TokenResponseDto(
          user.getUserId(),
          user.getEmail(),
          user.getName(),
          user.getRole().name(),
          accessToken,
          refreshToken
      );
    } catch (AuthenticationCredentialsNotFoundException ex) {
      log.warn("[AuthService] 로그인 실패 - 인증 실패");
      throw ApiException.of(HttpStatus.BAD_REQUEST, "이메일 또는 비밀번호가 올바르지 않습니다.");
    }
  }

  @Override
  @Transactional
  public TokenResponseDto refreshToken(String refreshToken) {
    log.info("[AuthService] 토큰 재발급 시도");

    // 리프레시 토큰이 없는 경우
    if (refreshToken == null || refreshToken.isBlank()) {
      log.warn("[AuthService] 토큰 재발급 실패 - 리프레시 토큰 없음");
      throw ApiException.of(HttpStatus.UNAUTHORIZED, ErrorMessage.MISSING_REFRESH_TOKEN);
    }

    // 리프레시 토큰 유효성 검증
    if (!jwtTokenProvider.validateToken(refreshToken)) {
      log.warn("[AuthService] 토큰 재발급 실패 status={}, reason={}",
          HttpStatus.UNAUTHORIZED.value(), ErrorMessage.INVALID_REFRESH_TOKEN);
      throw ApiException.of(HttpStatus.UNAUTHORIZED, ErrorMessage.INVALID_REFRESH_TOKEN);
    }

    Long userId = jwtTokenProvider.getUserIdFromToken(refreshToken);

    // Redis에서 리프레시 토큰 조회하여 검증
    String redisKey = "RT:" + userId;
    String storedToken = redisDao.getRefreshToken(redisKey);

    if (storedToken == null) {
      log.error("[AuthService] 토큰 재발급 실패 - 저장된 리프레시 토큰 없음, userId={}", userId);
      throw ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.REFRESH_TOKEN_NOT_FOUND);
    }

    if (!storedToken.equals(refreshToken)) {
      log.warn("[AuthService] 토큰 재발급 실패 - 토큰 불일치, userId={}", userId);
      throw ApiException.of(HttpStatus.UNAUTHORIZED, ErrorMessage.INVALID_REFRESH_TOKEN);
    }

    // JWT에서 이메일과 역할 정보 추출 (DB 조회 없이)
    String email = jwtTokenProvider.getEmailFromToken(refreshToken);
    String role = jwtTokenProvider.getRoleFromToken(refreshToken);

    String newAccessToken = jwtTokenProvider.createAccessToken(userId, email, role);
    String newRefreshToken = jwtTokenProvider.createRefreshToken(userId, email, role);

    // Redis에 새로운 리프레시 토큰 저장
    redisDao.setRefreshToken(redisKey, newRefreshToken, refreshTokenValidityInSeconds);

    log.info("[AuthService] 토큰 재발급 성공, userId={}", userId);

    return new TokenResponseDto(
        userId,
        email,
        null, // 이름은 필요시에만 조회
        role,
        newAccessToken,
        newRefreshToken
    );
  }


  @Override
  @Transactional
  public void logout(Long userId) {
    log.info("[AuthService] 로그아웃 시도 userId={}", userId);
    String redisKey = "RT:" + userId;
    redisDao.deleteRefreshToken(redisKey);
    // 기기 연동 토큰도 정리(DB+Redis)
    try {
      firebasePushService.deleteToken(userId);
    } catch (Exception e) {
      log.warn("[AuthService] 로그아웃 중 FCM 토큰 제거 경고 - userId={}", userId, e);
    }
    log.info("[AuthService] 로그아웃 완료 userId={}", userId);
  }


  @Override
  @Transactional(readOnly = true)
  public boolean isEmailAvailable(String email) {
    log.info("[AuthService] 이메일 중복 확인");
    return !userRepository.existsByEmail(email);
  }

  @Override
  @Transactional
  public void changePassword(Long userId, ChangePasswordDto changePasswordDto) {
    log.info("[AuthService] 비밀번호 변경 시도");
    
    // 사용자 조회
    User user = userRepository.findById(userId).orElseThrow(() -> {
      log.warn("[AuthService] 비밀번호 변경 실패 - 유저 없음");
      return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.USER_NOT_FOUND);
    });
    
    // 기존 비밀번호 일치 여부 확인
    if (!passwordEncoder.matches(changePasswordDto.oldPassword(), user.getPassword())) {
      log.warn("[AuthService] 비밀번호 변경 실패 - 기존 비밀번호 불일치");
      throw ApiException.of(HttpStatus.UNAUTHORIZED, ErrorMessage.INVALID_PASSWORD);
    }
    
    // 새 비밀번호가 기존 비밀번호와 동일한지 확인
    if (passwordEncoder.matches(changePasswordDto.newPassword(), user.getPassword())) {
      log.warn("[AuthService] 비밀번호 변경 실패 - 이전과 동일한 비밀번호");
      throw ApiException.of(HttpStatus.UNPROCESSABLE_ENTITY, ErrorMessage.SAME_PASSWORD);
    }
    
    // 비밀번호 변경
    user.changePassword(passwordEncoder.encode(changePasswordDto.newPassword()));
    log.info("[AuthService] 비밀번호 변경 완료");
  }

}
