package com.ssafy.s13p21b204.interview.service;

import com.ssafy.s13p21b204.global.util.S3Util.S3UploadInfo;
import com.ssafy.s13p21b204.interview.dto.DemoInterviewResponseDto;
import com.ssafy.s13p21b204.interview.dto.InterviewEndRequestDto;
import com.ssafy.s13p21b204.interview.dto.InterviewEndResponseDto;
import com.ssafy.s13p21b204.interview.dto.InterviewQuestionResponseDto;
import com.ssafy.s13p21b204.interview.dto.InterviewReplyRequestDto;
import com.ssafy.s13p21b204.interview.dto.InterviewRequestDto;
import com.ssafy.s13p21b204.interview.dto.InterviewResponseDto;
import com.ssafy.s13p21b204.interview.dto.RelatedQuestionResponseDto;
import com.ssafy.s13p21b204.resume.entity.Resume;
import java.util.List;

public interface InterviewService {

    /**
     * VR 면접 질문 Presigned URL 조회
     * 면접 상태를 NOT_STARTED에서 IN_PROGRESS로 변경합니다.
     * 
     * @param interviewId 면접 ID
     * @param userId 사용자 ID (권한 검증용)
     * @return 질문 Presigned URL 리스트
     */
    List<InterviewQuestionResponseDto> getQuestionPresignedUrls(Long interviewId, Long userId);

    /**
     * 질문에 대한 유저의 답변을 등록 (꼬리질문 없음)
     */
    void registerReply(Long userId, InterviewReplyRequestDto interviewReplyRequestDto);

    /**
     * 질문에 대한 유저의 답변을 등록하고 꼬리질문 생성 및 반환
     */
    RelatedQuestionResponseDto registerReplyWithRelatedQuestion(Long userId, InterviewReplyRequestDto interviewReplyRequestDto);

    /**
     * 인터뷰 오디오 파일(.wav) 업로드용 Presigned URL 발급
     *
     * @param fileName 원본 파일명 (.wav 확장자 포함)
     * @return S3 업로드 정보 (presignedUrl, s3Key, expirationSeconds)
     */
    S3UploadInfo generateInterviewAudioUploadUrl(String fileName);


    /**
     * 면접 종료 및 리포트 생성
     */
    void endInterview(Long userId, InterviewEndRequestDto interviewEndRequestDto);

    /**
     * 시연용 면접 생성 (1분 자기소개)
     * 
     * @param userId 사용자 ID
     * @param jobPostingId 채용 공고 ID (프론트에서 전달)
     * @return 시연용 면접 정보 (interviewId, questionId, presignedUrl, exampleIntroduction)
     */
    DemoInterviewResponseDto createDemoInterview(Long userId, Long jobPostingId);
}
