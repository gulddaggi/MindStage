package com.ssafy.s13p21b204.watch.entity;

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

@Entity
@Builder
@AllArgsConstructor
@NoArgsConstructor(access = AccessLevel.PROTECTED)
@Getter
@Table(name = "galaxy_watchs")
public class GalaxyWatch {
  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long galaxyWatchId;

  @Column(nullable = false)
  private Long userId;

  @Column(nullable = false)
  private String modelName;

  @Column(nullable = false, unique = true)
  private String uuid;

  public void changeModel(String modelName, String uuid) {
    this.modelName = modelName;
    this.uuid = uuid;
  }

}
