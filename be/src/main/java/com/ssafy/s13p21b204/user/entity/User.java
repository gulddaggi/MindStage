package com.ssafy.s13p21b204.user.entity;

import com.ssafy.s13p21b204.global.entity.BaseEntity;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.validation.constraints.AssertTrue;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.springframework.http.HttpStatus;

@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
@Builder
@Entity
@AllArgsConstructor
@Table(name = "users")
public class User extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long userId;

  @Column(nullable = false, unique = true)
  private String email;

  @Column
  private String password;

  @Column(nullable = false)
  private String name;

  @Column(nullable = false)
  @Builder.Default
  private Boolean watchEnabled = false;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false)
  private Role role = Role.VISITOR;

  public enum Role {
    // VISITOR = 회원가입 직후 기본 권한, GENERAL = 일반 사용자, ADMIN = 관리자
    GENERAL, ADMIN, VISITOR
  }


  @AssertTrue(message = "LOCAL 계정은 비밀번호가 필수입니다.")
  public boolean isPasswordValid() {
    return this.password != null && !this.password.isBlank();
  }

  public void changePassword(String encodedPassword) {
    this.password = encodedPassword;
  }

  public void changeRole(Role newRole) {
    this.role = newRole;
  }

  public void changeEmail(String email) {
    this.email = email;
  }

  public void changeName(String name) {
    this.name = name;
  }

  public void changeWatchEnabled(boolean enabled) {
    this.watchEnabled = enabled;
  }
}
