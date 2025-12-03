package com.ssafy.s13p21b204.global.exception;

public class ErrorMessage {

  // 400 Bad Request
  public static final String BAD_REQUEST = "요청 파라미터가 올바르지 않습니다.";

  // 401 Unauthorized (인증 실패 / 토큰 문제)
  public static final String UNAUTHORIZED = "인증이 필요합니다.";
  public static final String REFRESH_TOKEN_NOT_FOUND = "해당 리프레시 토큰을 찾을 수 없습니다.";
  public static final String INVALID_TOKEN = "유효하지 않은 에세스 토큰입니다. 다시 로그인해주세요.";
  public static final String MISSING_TOKEN = "인증 토큰이 필요합니다. 로그인해주세요.";
  public static final String MISSING_REFRESH_TOKEN = "리프레시 토큰이 없습니다.";
  public static final String INVALID_REFRESH_TOKEN = "유효하지 않은 리프레시 토큰입니다.";
  public static final String INVALID_PASSWORD = "기존 비밀번호가 일치하지 않습니다.";
  public static final String S3_UPLOAD_TICKET_INVALID = "유효하지 않거나 만료된 파일 업로드 티켓입니다.";
  public static final String BAD_CREDENTIAL_REQUEST="이메일 또는 비밀번호가 올바르지 않습니다.";

  // 403 Forbidden (권한 없음)
  public static final String ACCESS_DENIED = "접근 권한이 없습니다.";

  // 404 Not Found (리소스 자체 부재)
  public static final String COMPANY_NOT_FOUND = "기업이 없습니다.";
  public static final String DEVICE_NOT_FOUND = "워치가 없습니다.";
  public static final String USER_NOT_FOUND = "해당 유저를 찾을 수 없습니다.";
  public static final String JOB_POSTING_NOT_FOUND = "채용 공고를 찾을 수 없습니다.";
  public static final String QUESTION_NOT_FOUND = "해당 질문을 찾을 수 없습니다.";
  public static final String RESUME_NOT_FOUND = "자소서가 없습니다.";
  public static final String INTERVIEW_NOT_FOUND = "면접이 없습니다.";
  public static final String INTERVIEW_QUESTIONS_NOT_FOUND = "면접 질문이 없습니다.";
  public static final String REPORT_NOT_FOUND = "레포트가 없습니다.";

  // 409 Conflict (상태/비즈니스 로직 충돌)
  public static final String EMAIL_ALREADY_EXISTS = "존재하는 이메일입니다.";
  public static final String DEVICE_ALREADY_REGISTERED = "이미 디바이스가 등록되었습니다.";
  public static final String JOB_POSTING_ALREADY_EXISTS = "이미 진행 중인 채용공고가 있습니다.";
  public static final String COMPANY_NAME_ALREADY_EXISTS = "이미 회사가 등록되어있습니다.";

  public static final String INTERVIEW_NOT_READY = "면접을 시작할 수 없는 상태입니다.";
  public static final String INTERVIEW_NOT_IN_PROGRESS = "면접이 진행 중인 상태가 아닙니다.";
  public static final String INTERVIEW_QUESTIONS_NOT_GENERATED = "질문이 생성되지 않았습니다.";
  public static final String REPORT_NOT_READY = "레포트가 아직 생성 중입니다.";

  // 410 Gone (만료된 리소스)
  public static final String JOB_POSTING_EXPIRED = "채용공고가 만료되었습니다.";
  public static final String MISSING_FCM_TOKEN = "해당 유저의 FCM 토큰이 없습니다.";
  public static final String EXPIRED_FCM_TOKEN = "저장된 FCM 토큰이 만료되어 제거되었습니다.";

  // 422 Unprocessable Entity (형식은 맞으나 비즈니스 규칙 위반)
  public static final String QUESTION_LIST_EMPTY = "질문이 최소 1개 이상 필요합니다.";
  public static final String ANSWER_TOO_SHORT = "답변은 최소 10자 이상 작성해주세요.";
  public static final String SAME_PASSWORD = "기존 비밀번호로 변경할 수 없습니다.";
  public static final String S3_FILE_VALIDATION_FAILED = "S3 파일 검증에 실패했습니다.";
  public static final String INTERVIEW_QUESTION_COUNT_MISMATCH = "질문 개수가 일치하지 않습니다.";

  // 500 Internal Server Error
  public static final String INTERNAL_SERVER_ERROR = "알 수 없는 서버 오류가 발생했습니다.";
  public static final String FCM_SEND_FAILED = "FCM 전송에 실패했습니다.";

  // 502 Bad Gateway (업스트림/AI 서비스 연동 실패)
  public static final String AI_SERVICE_CALL_FAILED = "AI 서비스 호출에 실패했습니다.";
  public static final String AI_SERVICE_CONNECTION_ERROR = "AI 서비스와의 연결에 실패했습니다.";

  // 504 Gateway Timeout (업스트림 타임아웃)
  public static final String AI_SERVICE_TIMEOUT = "AI 서비스 응답 시간이 초과되었습니다.";
}
