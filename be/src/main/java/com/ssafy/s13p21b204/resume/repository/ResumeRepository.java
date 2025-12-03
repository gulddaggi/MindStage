package com.ssafy.s13p21b204.resume.repository;

import com.ssafy.s13p21b204.resume.dto.ResumeWithInterviewIdProjection;
import com.ssafy.s13p21b204.resume.entity.Resume;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.EntityGraph;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

@Repository
public interface ResumeRepository extends JpaRepository<Resume, Long> {

  /**
   *  본인의 자소서 목록을 조회하는 쿼리
   */
  @EntityGraph(attributePaths = {"jobPosting","jobPosting.company"})
  List<Resume> findByUserId(Long userId);

  /**
   * Resume 단건 조회 시 JobPosting 및 Company를 함께 로딩
   */
  @Query("select r from Resume r join fetch r.jobPosting jp join fetch jp.company where r.resumeId = :resumeId")
  Optional<Resume> findByIdWithJobPostingAndCompany(@Param("resumeId") Long resumeId);

  /**
   * 본인의 자소서 목록을 Interview ID와 ProgressStatus와 함께 조회 (fetch join 사용)
   * DTO Projection을 사용하여 타입 안전성 보장
   * Interview가 없는 경우 null 반환
   */
  @Query("""
      SELECT r as resume, i.interviewId as interviewId, i.progressStatus as progressStatus
      FROM Resume r 
      LEFT JOIN FETCH r.jobPosting jp 
      LEFT JOIN FETCH jp.company 
      LEFT JOIN Interview i ON i.resume.resumeId = r.resumeId
      WHERE r.userId = :userId
      """)
  List<ResumeWithInterviewIdProjection> findByUserIdWithInterviewId(@Param("userId") Long userId);

}
