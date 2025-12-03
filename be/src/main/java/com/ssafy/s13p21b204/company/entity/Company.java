package com.ssafy.s13p21b204.company.entity;

import com.ssafy.s13p21b204.global.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.SQLDelete;
import org.hibernate.annotations.SQLRestriction;

@Entity
@NoArgsConstructor
@AllArgsConstructor(access = AccessLevel.PROTECTED)
@Getter
@Builder
@Table(name = "companies")
@SQLDelete(sql = "UPDATE companies SET status = 'DELETED', deleted_at = NOW() WHERE company_id = ?")
@SQLRestriction("deleted_at IS NULL")
public class Company extends BaseEntity {
  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long companyId;

  @Column(nullable = false)
  private String name;

  @Column(nullable = false)
  @Builder.Default
  private Size size = Size.ENTERPRISE;

  public enum Size{
    ENTERPRISE, // 대기업
    MID_SIZED, // 중견기업
    SME, // 중소기업
    STARTUP // 스타트업
  }
}
