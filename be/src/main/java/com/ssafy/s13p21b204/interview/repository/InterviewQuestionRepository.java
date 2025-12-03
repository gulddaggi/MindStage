package com.ssafy.s13p21b204.interview.repository;

import com.ssafy.s13p21b204.interview.entity.InterviewQuestion;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface InterviewQuestionRepository extends JpaRepository<InterviewQuestion, Long> {

  List<InterviewQuestion> findByInterviewId(Long interviewId);

}
