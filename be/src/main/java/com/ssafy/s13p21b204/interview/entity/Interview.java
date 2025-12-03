package com.ssafy.s13p21b204.interview.entity;

import com.ssafy.s13p21b204.global.entity.ProgressStatus;
import com.ssafy.s13p21b204.resume.entity.Resume;
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
@Table(name = "interviews")
@Builder
@AllArgsConstructor
@NoArgsConstructor(access = AccessLevel.PROTECTED)
@Getter
@EntityListeners(AuditingEntityListener.class)
public class Interview {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long interviewId;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "resume_id")
    private Resume resume;

    @Column(name = "company_id")
    private Long companyId;

    @CreatedDate
    private LocalDateTime createdAt;

    @Column(nullable = false)
    @Builder.Default
    private Boolean relatedQuestion = true;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private ProgressStatus progressStatus = ProgressStatus.NOT_STARTED;

    public void start() {
        this.progressStatus = ProgressStatus.IN_PROGRESS;
    }

    public void complete() {
        this.progressStatus = ProgressStatus.COMPLETED;
    }

    public void setRelatedQuestion(Boolean relatedQuestion) {
        this.relatedQuestion = relatedQuestion != null ? relatedQuestion : Boolean.TRUE;
    }

    public void markReady() {
        this.progressStatus = ProgressStatus.NOT_STARTED;
    }

    public void markAsFailed() {
        this.progressStatus = ProgressStatus.FAILED;
    }

    public void markAsReported() {
        this.progressStatus = ProgressStatus.REPORTED;
    }

}
