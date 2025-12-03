package com.ssafy.s13p21b204.security;

import com.ssafy.s13p21b204.user.entity.User;
import com.ssafy.s13p21b204.user.entity.User.Role;
import com.fasterxml.jackson.annotation.JsonIgnore;
import java.util.Collection;
import java.util.List;
import lombok.Getter;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.userdetails.UserDetails;

@Getter
@RequiredArgsConstructor
public class UserPrincipal implements UserDetails {

  private final Long userId;
  private final String email;
  private final Role role;

  @JsonIgnore
  private final String password;

  public static UserPrincipal from(User user) {
    return new UserPrincipal(
        user.getUserId(),
        user.getEmail(),
        user.getRole(),
        user.getPassword()
    );
  }

  @Override
  public Collection<? extends GrantedAuthority> getAuthorities() {
    return List.of(new SimpleGrantedAuthority("ROLE_" + role.name()));
  }

  @Override
  public String getUsername() {
    return email;
  }

  @Override
  public boolean isAccountNonExpired() {
    return true;
  }

  @Override
  public boolean isAccountNonLocked() {
    return true;
  }

  @Override
  public boolean isCredentialsNonExpired() {
    return true;
  }

  @Override
  public boolean isEnabled() {
    return true;
  }

}
