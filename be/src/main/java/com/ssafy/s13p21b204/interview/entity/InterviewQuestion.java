package com.ssafy.s13p21b204.interview.entity;

import com.fasterxml.jackson.annotation.JsonIgnore;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EntityListeners;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import java.time.LocalDateTime;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.jpa.domain.support.AuditingEntityListener;

@Entity
@Table(name = "interview_questions")
@Getter
@NoArgsConstructor
@Builder
@AllArgsConstructor(access = AccessLevel.PROTECTED)
@EntityListeners(AuditingEntityListener.class)
public class InterviewQuestion {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long interviewQuestionId;

  @ManyToOne(fetch = FetchType.LAZY)
  @JoinColumn(name = "parent_questoin_id")
  @JsonIgnore
  private InterviewQuestion parentQuestionId;

  @Column(nullable = false)
  private Long interviewId;

  @Column(nullable = false, columnDefinition = "TEXT")
  private String content;

  @Column(nullable = false)
  private String s3Key;

  @Enumerated(EnumType.STRING)
  @Builder.Default
  private Difficult difficult = Difficult.LAX;

  @CreatedDate
  private LocalDateTime createdAt;

  public enum Difficult {
    LAX, // 쉬운
    STRICT, // 엄격한
  }

}
