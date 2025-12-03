package com.ssafy.s13p21b204.interview.repository;

import com.ssafy.s13p21b204.interview.entity.Reply;
import java.util.Collection;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface ReplyRepository extends JpaRepository<Reply, Long> {

  Optional<Reply> findByInterviewQuestionInterviewQuestionId(Long interviewQuestionId);

  List<Reply> findByInterviewQuestionInterviewQuestionIdIn(Collection<Long> interviewQuestionIds);

}