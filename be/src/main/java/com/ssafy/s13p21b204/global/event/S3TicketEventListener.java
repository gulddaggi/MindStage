package com.ssafy.s13p21b204.global.event;

import com.ssafy.s13p21b204.global.util.S3Util;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;
import org.springframework.transaction.event.TransactionPhase;
import org.springframework.transaction.event.TransactionalEventListener;

@Component
@RequiredArgsConstructor
@Slf4j
public class S3TicketEventListener {

  private final S3Util s3Util;

  @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
  public void handleS3TicketConsume(S3TicketConsumeEvent event) {
    log.info("[S3TicketEventListener] 트랜잭션 커밋 완료 - 티켓 소비 시작");
    s3Util.consumeTicket(event.s3Key());
  }
}