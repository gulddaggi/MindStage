package com.ssafy.s13p21b204.security.service;

import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.security.UserPrincipal;
import com.ssafy.s13p21b204.user.entity.User;
import com.ssafy.s13p21b204.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.security.core.userdetails.UserDetails;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.core.userdetails.UsernameNotFoundException;
import org.springframework.stereotype.Service;

@Slf4j
@Service
@RequiredArgsConstructor
public class CustomUserDetailsService implements UserDetailsService {

  private final UserRepository userRepository;

  @Override
  public UserDetails loadUserByUsername(String email) throws UsernameNotFoundException {
    log.info("[UserDetailsService] 사용자 인증 정보 조회 시도");

    User user = userRepository.findByEmail(email)
        .orElseThrow(() -> {
          log.warn("[UserDetailsService] 사용자를 찾을 수 없음 Status = {}, message={}", HttpStatus.NOT_FOUND.value(), ErrorMessage.USER_NOT_FOUND);
          return new UsernameNotFoundException("해당 이메일을 가진 유저를 찾을 수 없습니다.");
        });

    log.info("[UserDetailsService] 사용자 인증 정보 조회 성공");

    return UserPrincipal.from(user);
  }
}
