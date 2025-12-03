package com.ssafy.s13p21b204.jobPosting.entity;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.ssafy.s13p21b204.company.entity.Company;
import com.ssafy.s13p21b204.global.entity.Status;
import com.ssafy.s13p21b204.question.entity.Question;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.OneToMany;
import jakarta.persistence.Table;
import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.List;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Entity
@Table(name = "job_postings")
@Getter
@Builder
@NoArgsConstructor
@AllArgsConstructor(access = AccessLevel.PROTECTED)
public class JobPosting {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long jobPostingId;

  @ManyToOne(fetch = FetchType.LAZY)
  @JoinColumn(name = "company_id", nullable = false)
  private Company company;

  @JsonIgnore
  @OneToMany(mappedBy = "jobPosting", fetch = FetchType.LAZY)
  @Builder.Default
  private List<Question> questions = new ArrayList<>();

  @Enumerated(EnumType.STRING)
  @Column(nullable = false)
  @Builder.Default
  private Part part = Part.SOFTWARE;

  @Column(nullable = false)
  private LocalDateTime createdAt;

  @Column(nullable = false)
  private LocalDateTime expiredAt;

  @Column(nullable = false)
  @Builder.Default
  private Status status = Status.ACTIVE;

  @Column(length = 500)
  private String s3PreferenceFileKey;  // S3에 저장된 직무 우대사항 PDF 파일 경로 (선택 사항)

  public enum Part {
    MARKETING, // 국내영업 마케팅
    SOFTWARE,  // SW개발 - 서버 소프트웨어
    QUALITY    // 품질/서비스-품질
  }

  public void PostingEnd() {
    this.status = Status.INACTIVE;
  }
}
