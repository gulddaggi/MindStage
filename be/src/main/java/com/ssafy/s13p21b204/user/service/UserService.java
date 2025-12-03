package com.ssafy.s13p21b204.user.service;

import com.ssafy.s13p21b204.user.dto.UserResponseDto;

public interface UserService {

  UserResponseDto findMe(Long userId);

  public void changeName(Long userId, String name);
}
