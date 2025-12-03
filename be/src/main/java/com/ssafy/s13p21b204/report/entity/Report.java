package com.ssafy.s13p21b204.report.entity;

import com.ssafy.s13p21b204.global.entity.ProgressStatus;
import com.ssafy.s13p21b204.global.entity.Status;
import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.index.CompoundIndex;
import org.springframework.data.mongodb.core.index.Indexed;
import org.springframework.data.mongodb.core.mapping.Document;

@Document(collection = "reports")
@Getter
@Builder
@AllArgsConstructor(access = AccessLevel.PROTECTED)
@NoArgsConstructor
@CompoundIndex(name = "interview_unique", def = "{'interviewId': 1}", unique = true)
public class Report {

  @Id
  private String reportId;

  @Indexed
  private Long interviewId;

  @Builder.Default
  private ProgressStatus progressStatus = ProgressStatus.CREATING;

  @Builder.Default
  private Map<String, Integer> scores = new HashMap<>();

  private String report;

  // 면접 Q&A 리스트 (질문, 답변, 평가 라벨 포함)
  // label: 2(부정), 1(긍정), 0(중립)
  @Builder.Default
  private List<QnaItem> qnaList = new ArrayList<>();

  @Builder.Default
  private Status status = Status.ACTIVE;

  @CreatedDate
  private LocalDateTime createdAt;

  private LocalDateTime deletedAt;

  /**
   * 소프트 딜리트 - Report를 삭제 상태로 표시
   * status를 DELETED로 변경하고 deletedAt을 현재 시간으로 설정
   * 두 필드를 항상 함께 업데이트하여 일관성 유지
   */
  public void markDeleted() {
    this.status = Status.DELETED;
    this.deletedAt = LocalDateTime.now();
  }

  /**
   * 삭제 여부 확인
   * status가 DELETED이거나 deletedAt이 null이 아니면 삭제된 것으로 간주
   * 주의: markDeleted() 메서드를 통해서만 삭제 상태를 변경해야 두 필드의 일관성이 보장됨
   * @return 삭제된 경우 true, 그렇지 않으면 false
   */
  public boolean isDeleted() {
    return this.status == Status.DELETED || this.deletedAt != null;
  }

  /**
   * 삭제 취소 - Report를 다시 활성화
   * status를 ACTIVE로 변경하고 deletedAt을 null로 설정
   * 두 필드를 항상 함께 업데이트하여 일관성 유지
   */
  public void restore() {
    this.status = Status.ACTIVE;
    this.deletedAt = null;
  }

}
