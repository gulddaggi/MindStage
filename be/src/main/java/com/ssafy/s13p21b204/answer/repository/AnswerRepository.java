package com.ssafy.s13p21b204.answer.repository;

import com.ssafy.s13p21b204.answer.entity.Answer;
import org.springframework.data.jpa.repository.EntityGraph;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface AnswerRepository extends JpaRepository<Answer, Long> {

  /**
   * 특정 자소서의 모든 답변을 질문 정보와 함께 조회
   * @param resumeId 자소서 ID
   * @return 답변 리스트 (질문 정보 포함)
   */
  @EntityGraph(attributePaths = {"question"})
  List<Answer> findByResumeId(Long resumeId);
}
