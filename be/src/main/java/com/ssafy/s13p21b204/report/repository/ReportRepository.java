package com.ssafy.s13p21b204.report.repository;

import com.ssafy.s13p21b204.report.entity.Report;
import java.util.List;
import java.util.Optional;
import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.data.mongodb.repository.Query;

public interface ReportRepository extends MongoRepository<Report, String> {

  /**
   * 인터뷰 ID로 리포트 조회 (삭제되지 않은 데이터만)
   * 여러 리포트가 존재할 수 있으므로 List로 반환
   * @param interviewId 인터뷰 ID
   * @return 리포트 리스트 (삭제되지 않은 것만)
   */
  @Query("{ 'interviewId': ?0, 'status': { $ne: 'DELETED' }, 'deletedAt': null }")
  List<Report> findAllByInterviewId(Long interviewId);

  /**
   * 여러 인터뷰 ID에 해당하는 리포트를 한 번에 조회 (N+1 쿼리 문제 해결)
   * 삭제되지 않은 데이터만 조회
   * @param interviewIds 인터뷰 ID 리스트
   * @return 해당 인터뷰 ID들에 대한 리포트 리스트 (삭제되지 않은 것만)
   */
  @Query("{ 'interviewId': { $in: ?0 }, 'status': { $ne: 'DELETED' }, 'deletedAt': null }")
  List<Report> findByInterviewIdIn(List<Long> interviewIds);

  /**
   * ID로 리포트 조회 (삭제되지 않은 데이터만)
   * 기본 findById를 오버라이드하여 삭제된 데이터는 조회되지 않도록 함
   * 주의: 이 메서드는 status != 'DELETED' && deletedAt == null 조건으로 필터링하므로,
   * 삭제된 리포트는 조회되지 않습니다. 삭제된 리포트도 조회하려면 findByIdIncludingDeleted()를 사용하세요.
   * @param reportId 리포트 ID
   * @return 리포트 (삭제되지 않은 경우만)
   */
  @Query("{ '_id': ?0, 'status': { $ne: 'DELETED' }, 'deletedAt': null }")
  Optional<Report> findById(String reportId);

  /**
   * 모든 리포트 조회 (삭제되지 않은 데이터만)
   * 기본 findAll을 오버라이드하여 삭제된 데이터는 조회되지 않도록 함
   * 주의: 이 메서드는 status != 'DELETED' && deletedAt == null 조건으로 필터링하므로,
   * 삭제된 리포트는 조회되지 않습니다.
   * @return 삭제되지 않은 모든 리포트 리스트
   */
  @Query("{ 'status': { $ne: 'DELETED' }, 'deletedAt': null }")
  List<Report> findAll();

  /**
   * 삭제된 데이터를 포함하여 인터뷰 ID로 리포트 조회 (관리자용)
   * @param interviewId 인터뷰 ID
   * @return 리포트 (삭제된 것 포함)
   */
  @Query("{ 'interviewId': ?0 }")
  Optional<Report> findByInterviewIdIncludingDeleted(Long interviewId);

  /**
   * 삭제된 데이터를 포함하여 ID로 리포트 조회 (관리자용)
   * @param reportId 리포트 ID
   * @return 리포트 (삭제된 것 포함)
   */
  @Query("{ '_id': ?0 }")
  Optional<Report> findByIdIncludingDeleted(String reportId);

}
