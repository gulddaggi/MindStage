package com.ssafy.s13p21b204.user.dto;


import com.ssafy.s13p21b204.user.entity.User;
import com.ssafy.s13p21b204.user.entity.User.Role;

public record UserResponseDto(
    Long userId,
    String email,
    String name,
    Role role
) {

  public static UserResponseDto from(User user) {
    return new UserResponseDto(
        user.getUserId(),
        user.getEmail(),
        user.getName(),
        user.getRole()
    );
  }
}
