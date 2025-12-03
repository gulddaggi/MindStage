package com.ssafy.s13p21b204.user.service.impl;

import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.user.dto.UserResponseDto;
import com.ssafy.s13p21b204.user.entity.User;
import com.ssafy.s13p21b204.user.repository.UserRepository;
import com.ssafy.s13p21b204.user.service.UserService;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Slf4j
public class UserServiceImpl implements UserService {

  private final UserRepository userRepository;

  @Transactional
  @Override
  public UserResponseDto findMe(Long userId) {
    log.info("[UserService] 유저 조회 시도");
    User u = userRepository.findById(userId)
        .orElseThrow(() -> {
          log.warn("[UserService] 유저 조회 실패");
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.USER_NOT_FOUND);
        });
    return UserResponseDto.from(u);
  }

  @Transactional
  @Override
  public void changeName(Long userId, String name) {
    log.info("[UserService] 유저 이름 변경 시도");
    User existingUser = userRepository.findById(userId).orElseThrow(() -> {
      log.warn("[UserService] 유저 이름 변경 실패 - 유저 없음");
      throw ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.USER_NOT_FOUND);
    });
    existingUser.changeName(name);
    log.info("[UserService] 유저 이름 변경 완료");
  }
}
