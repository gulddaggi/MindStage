package com.ssafy.s13p21b204.global.util;

import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.redis.RedisDao;
import java.time.Duration;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Component;
import software.amazon.awssdk.services.s3.S3Client;
import software.amazon.awssdk.services.s3.model.GetObjectRequest;
import software.amazon.awssdk.services.s3.model.HeadObjectRequest;
import software.amazon.awssdk.services.s3.model.NoSuchKeyException;
import software.amazon.awssdk.services.s3.model.PutObjectRequest;
import software.amazon.awssdk.services.s3.presigner.S3Presigner;
import software.amazon.awssdk.services.s3.presigner.model.GetObjectPresignRequest;
import software.amazon.awssdk.services.s3.presigner.model.PresignedGetObjectRequest;
import software.amazon.awssdk.services.s3.presigner.model.PresignedPutObjectRequest;
import software.amazon.awssdk.services.s3.presigner.model.PutObjectPresignRequest;

/**
 * S3 파일 업로드/다운로드를 위한 범용 유틸리티
 *
 * Redis 티켓 시스템을 통해 업로드 보안을 강화합니다.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class S3Util {

  private final S3Presigner s3Presigner;
  private final S3Client s3Client;
  private final RedisDao redisDao;

  @Value("${cloud.aws.s3.bucket}")
  private String bucketName;

  // Presigned URL 만료 시간 (15분)
  private static final long PRESIGNED_URL_EXPIRATION_SECONDS = 900;

  // Redis 티켓 만료 시간 (15분)
  private static final long TICKET_TTL_SECONDS = 900;

  // Redis Key 접두사
  private static final String TICKET_KEY_PREFIX = "s3_ticket:";

  /**
   * S3 업로드용 Presigned URL 발급 (Redis 티켓 포함)
   *
   * @param directory S3 디렉토리 경로 (예: "job-postings", "profiles")
   * @param fileName 원본 파일명
   * @return S3UploadInfo { presignedUrl, s3Key, expirationSeconds }
   */
  public S3UploadInfo generateUploadPresignedUrl(String directory, String fileName) {
    try {
      // 고유한 파일명 생성 (UUID + 원본 파일명)
      String uniqueFileName = UUID.randomUUID() + "_" + fileName;

      // S3 Key 생성 (디렉토리 경로 포함)
      String s3Key = directory != null && !directory.isBlank()
          ? directory + "/" + uniqueFileName
          : uniqueFileName;

      // S3 Presigned URL 생성
      PutObjectRequest putObjectRequest = PutObjectRequest.builder()
          .bucket(bucketName)
          .key(s3Key)
          .build();

      PutObjectPresignRequest presignRequest = PutObjectPresignRequest.builder()
          .signatureDuration(Duration.ofSeconds(PRESIGNED_URL_EXPIRATION_SECONDS))
          .putObjectRequest(putObjectRequest)
          .build();

      PresignedPutObjectRequest presignedRequest = s3Presigner.presignPutObject(presignRequest);
      String presignedUrl = presignedRequest.url().toString();

      // Redis에 업로드 티켓 저장 (TTL: 15분)
      String ticketKey = TICKET_KEY_PREFIX + s3Key;
      redisDao.setS3UploadTicket(ticketKey, "ISSUED", TICKET_TTL_SECONDS);

      log.info("[S3Util] Upload Presigned URL 발급 완료 - s3Key: {}", s3Key);

      return new S3UploadInfo(presignedUrl, s3Key, PRESIGNED_URL_EXPIRATION_SECONDS);

    } catch (Exception e) {
      log.error("[S3Util] Presigned URL 발급 실패: {}", e.getMessage(), e);
      throw ApiException.of(HttpStatus.INTERNAL_SERVER_ERROR, ErrorMessage.INTERNAL_SERVER_ERROR);
    }
  }

  /**
   * S3 다운로드용 Presigned URL 발급 (티켓 불필요)
   *
   * @param s3Key S3 객체 키
   * @return Presigned URL
   */
  public String generateDownloadPresignedUrl(String s3Key) {
    try {
      GetObjectRequest getObjectRequest = GetObjectRequest.builder()
          .bucket(bucketName)
          .key(s3Key)
          .build();

      GetObjectPresignRequest presignRequest = GetObjectPresignRequest.builder()
          .signatureDuration(Duration.ofSeconds(PRESIGNED_URL_EXPIRATION_SECONDS))
          .getObjectRequest(getObjectRequest)
          .build();

      PresignedGetObjectRequest presignedRequest = s3Presigner.presignGetObject(presignRequest);

      log.info("[S3Util] Download Presigned URL 발급 완료 - s3Key: {}", s3Key);

      return presignedRequest.url().toString();

    } catch (Exception e) {
      log.error("[S3Util] Download Presigned URL 발급 실패: {}", e.getMessage(), e);
      throw ApiException.of(HttpStatus.INTERNAL_SERVER_ERROR, ErrorMessage.INTERNAL_SERVER_ERROR);
    }
  }

  /**
   * S3 업로드 티켓 검증 (삭제하지 않음)
   *
   * 서비스 로직 시작 전에 호출하여 유효한 티켓인지만 확인합니다.
   * 트랜잭션이 실패하면 티켓이 보존되어 재사용 가능합니다.
   *
   * @param s3Key 검증할 S3 Key
   * @throws ApiException 티켓이 유효하지 않으면 예외 발생
   */
  public void validateS3Ticket(String s3Key) {
    String ticketKey = TICKET_KEY_PREFIX + s3Key;
    String ticket = redisDao.getS3UploadTicket(ticketKey);

    if (ticket == null || ticket.isBlank()) {
      log.warn("[S3Util] 유효하지 않은 S3 업로드 티켓 - s3Key: {}", s3Key);
      throw ApiException.of(HttpStatus.UNAUTHORIZED, ErrorMessage.S3_UPLOAD_TICKET_INVALID);
    }

    log.info("[S3Util] S3 업로드 티켓 검증 완료 (소비 전) - s3Key: {}", s3Key);
  }

  /**
   * S3 업로드 티켓 소비 (삭제)
   *
   * 트랜잭션이 성공적으로 커밋된 후에만 호출됩니다.
   * TransactionalEventListener를 통해 자동으로 호출됩니다.
   *
   * @param s3Key 소비할 S3 Key
   */
  public void consumeTicket(String s3Key) {
    String ticketKey = TICKET_KEY_PREFIX + s3Key;
    redisDao.deleteS3UploadTicket(ticketKey);
    log.info("[S3Util] S3 업로드 티켓 소비 완료 - s3Key: {}", s3Key);
  }

  /**
   * Redis에서 S3 업로드 티켓 검증 및 삭제 (1회용)
   *
   * @deprecated 이벤트 기반 방식으로 대체되었습니다.
   *             대신 validateS3Ticket()과 이벤트 발행을 사용하세요.
   * @param s3Key 검증할 S3 Key
   * @throws ApiException 티켓이 유효하지 않으면 예외 발생
   */
  @Deprecated
  public void validateAndConsumeS3Ticket(String s3Key) {
    validateS3Ticket(s3Key);
    consumeTicket(s3Key);
  }

  /**
   * S3 파일 존재 여부 확인 (HeadObject 사용)
   * 
   * @param s3Key 확인할 S3 Key
   * @return 파일이 존재하면 true, 없으면 false
   */
  public boolean doesObjectExist(String s3Key) {
    try {
      s3Client.headObject(HeadObjectRequest.builder()
          .bucket(bucketName)
          .key(s3Key)
          .build());
      log.debug("[S3Util] S3 파일 존재 확인 - s3Key: {}", s3Key);
      return true;
    } catch (NoSuchKeyException e) {
      log.warn("[S3Util] S3 파일 없음 - s3Key: {}", s3Key);
      return false;
    } catch (Exception e) {
      log.error("[S3Util] S3 파일 존재 확인 실패 - s3Key: {}", s3Key, e);
      return false; // 에러 발생 시 안전하게 false 반환
    }
  }

  /**
   * S3 업로드 정보를 담는 레코드
   *
   * @param presignedUrl 클라이언트가 업로드할 때 사용할 URL
   * @param s3Key S3에 저장될 파일의 전체 경로 (디렉토리 포함)
   * @param expirationSeconds URL 만료 시간 (초)
   */
  public record S3UploadInfo(
      String presignedUrl,
      String s3Key,
      long expirationSeconds
  ) {}
}