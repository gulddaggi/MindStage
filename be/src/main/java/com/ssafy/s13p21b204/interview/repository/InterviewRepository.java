package com.ssafy.s13p21b204.interview.repository;

import com.ssafy.s13p21b204.interview.entity.Interview;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

@Repository
public interface InterviewRepository extends JpaRepository<Interview, Long> {

  Optional<Interview> findByResumeResumeId(Long resumeId);

  @Query("SELECT i FROM Interview i JOIN FETCH i.resume r JOIN FETCH r.jobPosting WHERE i.interviewId = :interviewId")
  Optional<Interview> findByIdWithResume(@Param("interviewId") Long interviewId);

  @Query("SELECT i FROM Interview i JOIN FETCH i.resume r JOIN FETCH r.jobPosting jp WHERE jp.jobPostingId = :jobPostingId")
  List<Interview> findByJobPostingId(@Param("jobPostingId") Long jobPostingId);

  @Query("SELECT i FROM Interview i JOIN FETCH i.resume r JOIN FETCH r.jobPosting jp JOIN FETCH jp.company WHERE r.userId = :userId")
  List<Interview> findByUserId(@Param("userId") Long userId);
}