package com.ssafy.s13p21b204.question.repository;

import com.ssafy.s13p21b204.question.entity.Question;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface QuestionRepository extends JpaRepository<Question, Long> {

  List<Question> findByJobPostingJobPostingId(Long JobPostingId);

}
