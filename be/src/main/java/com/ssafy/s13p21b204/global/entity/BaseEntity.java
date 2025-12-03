package com.ssafy.s13p21b204.global.entity;

import jakarta.persistence.Column;
import jakarta.persistence.EntityListeners;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.MappedSuperclass;
import jakarta.persistence.PrePersist;
import java.time.LocalDateTime;
import lombok.Getter;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.LastModifiedDate;
import org.springframework.data.jpa.domain.support.AuditingEntityListener;

@Getter
@MappedSuperclass
@EntityListeners(AuditingEntityListener.class)
public class BaseEntity {

  @Enumerated(EnumType.STRING)
  @Column(nullable = false)
  protected Status status;

  @CreatedDate
  @Column(nullable = false, updatable = false)
  protected LocalDateTime createdAt;

  @LastModifiedDate
  @Column
  protected LocalDateTime updatedAt;

  @Column
  protected LocalDateTime deletedAt;

  @PrePersist
  protected void onCreate() {
    if (status == null) {
      this.status = Status.ACTIVE;
    }
  }

  public void markDeleted() {
    this.status = Status.DELETED;
    this.deletedAt = LocalDateTime.now();
  }
}
