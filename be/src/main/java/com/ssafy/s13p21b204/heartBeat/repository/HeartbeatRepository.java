package com.ssafy.s13p21b204.heartBeat.repository;

import com.ssafy.s13p21b204.heartBeat.entity.Heartbeat;
import org.springframework.data.mongodb.repository.MongoRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface HeartbeatRepository extends MongoRepository<Heartbeat, String> {

  // 특정 면접의 모든 심박수 데이터 조회 (측정 시간 오름차순)
  List<Heartbeat> findByInterviewIdOrderByMeasuredAtAsc(Long interviewId);

  // 특정 사용자의 모든 심박수 데이터 조회 (최근순)
//  List<Heartbeat> findByUserIdOrderByMeasuredAtDesc(Long userId);

  // 면접 ID로 데이터 개수 카운트
  long countByInterviewId(Long interviewId);

  // 면접 ID로 데이터 삭제 (GDPR 준수 등)
  void deleteByInterviewId(Long interviewId);
}
