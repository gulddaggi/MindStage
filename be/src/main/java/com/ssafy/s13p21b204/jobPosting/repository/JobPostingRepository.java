package com.ssafy.s13p21b204.jobPosting.repository;

import com.ssafy.s13p21b204.global.entity.Status;
import com.ssafy.s13p21b204.jobPosting.entity.JobPosting;
import com.ssafy.s13p21b204.jobPosting.entity.JobPosting.Part;
import java.time.LocalDateTime;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

@Repository
public interface JobPostingRepository extends JpaRepository<JobPosting, Long> {

  List<JobPosting> findByCompanyCompanyId(Long companyId);

  List<JobPosting> findByExpiredAtAfter(LocalDateTime currentTime);

  // 같은 회사의 같은 직무에 대해 활성 상태인 채용공고 조회
  Optional<JobPosting> findByCompany_CompanyIdAndPartAndStatus(
      Long companyId, Part part, Status status);

  @Query("SELECT DISTINCT jp FROM JobPosting jp " +
         "JOIN FETCH jp.company " +
         "LEFT JOIN FETCH jp.questions " +
         "WHERE jp.expiredAt > :currentTime")
  List<JobPosting> findActiveJobPostingsWithQuestions(@Param("currentTime") LocalDateTime currentTime);
}
