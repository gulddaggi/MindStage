package com.ssafy.s13p21b204.jobPosting.dto;

import com.ssafy.s13p21b204.jobPosting.entity.JobPosting;
import com.ssafy.s13p21b204.jobPosting.entity.JobPosting.Part;
import com.ssafy.s13p21b204.question.dto.QuestionResponseDto;
import java.util.List;

public record JobPostingResponseDto(
    Long jobPostingId,  
    String companyName,
    Part part,
    List<QuestionResponseDto> questionResponseDtos
) {
  public static JobPostingResponseDto of(JobPosting jobPosting) {
    List<QuestionResponseDto> questions = jobPosting.getQuestions()
        .stream()
        .map(QuestionResponseDto::of)
        .toList();
        
    return new JobPostingResponseDto(
        jobPosting.getJobPostingId(),
        jobPosting.getCompany().getName(),
        jobPosting.getPart(),
        questions
    );
  }

}
