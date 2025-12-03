package com.ssafy.s13p21b204.interview.event;

import com.ssafy.s13p21b204.interview.dto.InterviewEndResponseDto;
import com.ssafy.s13p21b204.report.service.ReportService;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Async;
import org.springframework.stereotype.Component;
import org.springframework.transaction.event.TransactionPhase;
import org.springframework.transaction.event.TransactionalEventListener;

@Component
@RequiredArgsConstructor
@Slf4j
public class InterviewEndedEventListener {

    private final ReportService reportService;

    /**
     * 면접 종료 이벤트 처리
     * 트랜잭션 커밋 후 비동기로 리포트 업데이트
     */
    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    @Async("interviewWorkflowExecutor")
    public void handleInterviewEnded(InterviewEndedEvent event) {
        log.info("[InterviewEndedEventListener] 면접 종료 이벤트 처리 시작 - interviewId={}", 
            event.interviewId());
        
        try {
            reportService.updateReport(event.interviewId(), event.aiEndInterviewResponse());
            log.info("[InterviewEndedEventListener] 리포트 업데이트 완료 - interviewId={}", 
                event.interviewId());
        } catch (Exception e) {
            log.error("[InterviewEndedEventListener] 리포트 업데이트 실패 - interviewId={}, error={}", 
                event.interviewId(), e.getMessage(), e);
        }
    }
}

