# MindStage Backend

MindStage는 VR 면접 시스템을 위한 백엔드 서비스입니다.

Spring Boot와 Java 17을 기반으로 구축되었으며, 면접 진행, 자소서 관리, 리포트 생성, 심박수 모니터링 등의 기능을 제공합니다.

## 프로젝트 개요

MindStage Backend는 VR 면접 플랫폼의 핵심 백엔드 시스템으로, 면접 진행 전 과정을 관리하고 AI 기반 리포트를 생성하는 서비스입니다.

### 주요 기능

- **면접 관리**: VR 면접 생성, 진행, 종료
- **자소서 관리**: 자소서 등록 및 관리
- **질문 관리**: 면접 질문 생성 및 관리
- **리포트 생성**: AI 기반 면접 리포트 생성
- **심박수 모니터링**: VR 면접 중 심박수 데이터 수집 및 분석
- **워치 연동**: 갤럭시 워치와의 연동 기능
- **인증/인가**: JWT 기반 사용자 인증 시스템

## 시스템 아키텍처

### 프로젝트 구조

```
be/
├── src/main/java/com/ssafy/s13p21b204/
│   ├── BeApplication.java              # Spring Boot 메인 애플리케이션 엔트리포인트
│   │
│   ├── answer/                         # 답변 모듈
│   │   ├── dto/
│   │   │   └── AnswerRequestDto.java   # 답변 요청 DTO
│   │   ├── entity/
│   │   │   └── Answer.java             # 답변 엔티티
│   │   ├── repository/
│   │   │   └── AnswerRepository.java   # 답변 레포지토리
│   │   └── service/
│   │       ├── AnswerService.java      # 답변 서비스 인터페이스
│   │       └── impl/
│   │           └── AnswerServiceImpl.java # 답변 서비스 구현체
│   │
│   ├── auth/                           # 인증/인가 모듈
│   │   ├── controller/
│   │   │   └── AuthController.java     # 로그인, 회원가입, 토큰 갱신 API
│   │   ├── dto/                        # 인증 관련 DTO
│   │   │   ├── ChangePasswordDto.java  # 비밀번호 변경 요청 DTO
│   │   │   ├── LoginRequestDto.java    # 로그인 요청 DTO
│   │   │   ├── SignUpRequestDto.java   # 회원가입 요청 DTO
│   │   │   └── TokenResponseDto.java   # JWT 토큰 응답 DTO
│   │   └── service/                    # 인증 서비스 계층
│   │       ├── AuthService.java        # 인증 서비스 인터페이스
│   │       └── impl/
│   │           └── AuthServiceImpl.java # 인증 서비스 구현체
│   │
│   ├── company/                        # 기업 관리 모듈
│   │   ├── controller/
│   │   │   └── CompanyAdminController.java # 기업 관리 API
│   │   ├── dto/
│   │   │   ├── CompanyRequestDto.java  # 기업 요청 DTO
│   │   │   └── CompanyResponseDto.java # 기업 응답 DTO
│   │   ├── entity/
│   │   │   └── Company.java            # 기업 엔티티
│   │   ├── repository/
│   │   │   └── CompanyRepository.java  # 기업 레포지토리
│   │   └── service/
│   │       ├── CompanyService.java     # 기업 서비스 인터페이스
│   │       └── impl/
│   │           └── CompanyServiceImpl.java # 기업 서비스 구현체
│   │
│   ├── global/                         # 전역 설정 및 공통 모듈
│   │   ├── config/                     # Spring 설정 클래스들
│   │   │   ├── AsyncConfig.java        # 비동기 처리 설정
│   │   │   ├── CacheConfig.java        # 캐시 설정
│   │   │   ├── RedisConfig.java        # Redis 설정
│   │   │   ├── S3Config.java           # AWS S3 설정
│   │   │   ├── SecurityConfig.java     # Spring Security 설정 (JWT, CORS, 인증/인가)
│   │   │   ├── SwaggerConfig.java      # Swagger/OpenAPI 3 문서화 설정
│   │   │   └── WebClientConfig.java    # WebClient 설정
│   │   ├── entity/                     # 공통 엔티티 및 enum
│   │   │   ├── BaseEntity.java         # 공통 베이스 엔티티
│   │   │   ├── ProgressStatus.java     # 진행 상태 enum
│   │   │   └── Status.java             # 일반적인 상태 관리 enum
│   │   ├── event/                      # 이벤트 처리
│   │   │   ├── S3TicketConsumeEvent.java # S3 티켓 소비 이벤트
│   │   │   └── S3TicketEventListener.java # S3 티켓 이벤트 리스너
│   │   ├── exception/                  # 예외 처리 관련 클래스
│   │   │   ├── ApiException.java       # 커스텀 API 예외 클래스
│   │   │   ├── ErrorMessage.java       # 에러 메시지 정의
│   │   │   └── GlobalExceptionHandler.java # 전역 예외 처리기 (@RestControllerAdvice)
│   │   ├── fastapi/                    # FastAPI 연동
│   │   │   ├── AiClient.java           # AI 서비스 WebClient 클라이언트
│   │   │   └── dto/                    # AI 서비스 DTO
│   │   ├── redis/                      # Redis 관련
│   │   │   ├── CacheNames.java         # 캐시 이름 정의
│   │   │   └── RedisDao.java           # Redis 데이터 접근 객체
│   │   └── util/                       # 유틸리티 클래스들
│   │       ├── ApiResult.java          # 표준 API 응답 래퍼 클래스
│   │       ├── CookieUtil.java         # 쿠키 처리 유틸리티
│   │       └── S3Util.java             # S3 유틸리티
│   │
│   ├── heartBeat/                      # 심박수 모니터링 모듈
│   │   ├── controller/
│   │   │   └── HeartBeatController.java # 심박수 데이터 API
│   │   ├── dto/
│   │   │   ├── HeartbeatBatchRequestDto.java # 심박수 배치 요청 DTO
│   │   │   ├── HeartbeatRequestDto.java # 심박수 요청 DTO
│   │   │   └── HeartbeatResponseDto.java # 심박수 응답 DTO
│   │   ├── entity/
│   │   │   └── Heartbeat.java          # 심박수 엔티티 (MongoDB)
│   │   ├── repository/
│   │   │   └── HeartbeatRepository.java # 심박수 레포지토리
│   │   └── service/
│   │       ├── HeartbeatService.java   # 심박수 서비스 인터페이스
│   │       └── impl/
│   │           └── HeartbeatServiceImpl.java # 심박수 서비스 구현체
│   │
│   ├── interview/                      # 면접 관리 모듈
│   │   ├── controller/
│   │   │   └── InterviewController.java # 면접 API 엔드포인트
│   │   ├── dto/                        # 면접 관련 DTO
│   │   │   ├── InterviewEndRequestDto.java # 면접 종료 요청 DTO
│   │   │   ├── InterviewEndResponseDto.java # 면접 종료 응답 DTO
│   │   │   ├── InterviewQuestionResponseDto.java # 면접 질문 응답 DTO
│   │   │   ├── InterviewReplyRequestDto.java # 면접 답변 요청 DTO
│   │   │   ├── InterviewRequestDto.java # 면접 요청 DTO
│   │   │   ├── InterviewResponseDto.java # 면접 응답 DTO
│   │   │   ├── RelatedQuestionResponseDto.java # 연관 질문 응답 DTO
│   │   │   └── VrInterviewRequestDto.java # VR 면접 요청 DTO
│   │   ├── entity/
│   │   │   ├── Interview.java          # 면접 엔티티
│   │   │   ├── InterviewQuestion.java  # 면접 질문 엔티티
│   │   │   └── Reply.java              # 답변 엔티티
│   │   ├── event/
│   │   │   ├── InterviewEndedEvent.java # 면접 종료 이벤트
│   │   │   └── InterviewEndedEventListener.java # 면접 종료 이벤트 리스너
│   │   ├── repository/
│   │   │   ├── InterviewQuestionRepository.java # 면접 질문 레포지토리
│   │   │   ├── InterviewRepository.java # 면접 레포지토리
│   │   │   └── ReplyRepository.java    # 답변 레포지토리
│   │   └── service/
│   │       ├── InterviewService.java   # 면접 서비스 인터페이스
│   │       └── impl/
│   │           └── InterviewServiceImpl.java # 면접 서비스 구현체
│   │
│   ├── jobPosting/                     # 채용 공고 모듈
│   │   ├── controller/
│   │   │   ├── JobPostingAdminController.java # 채용 공고 관리 API
│   │   │   └── JobPostingController.java # 채용 공고 API
│   │   ├── dto/
│   │   │   ├── JobPostingRegisterDto.java # 채용 공고 등록 DTO
│   │   │   └── JobPostingResponseDto.java # 채용 공고 응답 DTO
│   │   ├── entity/
│   │   │   └── JobPosting.java         # 채용 공고 엔티티
│   │   ├── repository/
│   │   │   └── JobPostingRepository.java # 채용 공고 레포지토리
│   │   └── service/
│   │       ├── JobPostingService.java  # 채용 공고 서비스 인터페이스
│   │       └── impl/
│   │           └── JobPostingServiceImpl.java # 채용 공고 서비스 구현체
│   │
│   ├── question/                       # 질문 관리 모듈
│   │   ├── dto/
│   │   │   ├── QuestionRequestDto.java # 질문 요청 DTO
│   │   │   └── QuestionResponseDto.java # 질문 응답 DTO
│   │   ├── entity/
│   │   │   └── Question.java           # 질문 엔티티
│   │   ├── repository/
│   │   │   └── QuestionRepository.java # 질문 레포지토리
│   │   └── service/
│   │       ├── QuestionService.java    # 질문 서비스 인터페이스
│   │       └── impl/
│   │           └── QuestionServiceImpl.java # 질문 서비스 구현체
│   │
│   ├── report/                         # 리포트 모듈
│   │   ├── controller/
│   │   │   └── ReportController.java   # 리포트 API 엔드포인트
│   │   ├── dto/
│   │   │   ├── ReportResponseDetailDto.java # 리포트 상세 응답 DTO
│   │   │   └── ReportResponseSummaryDto.java # 리포트 요약 응답 DTO
│   │   ├── entity/
│   │   │   ├── QnaItem.java            # Q&A 항목 엔티티
│   │   │   └── Report.java             # 리포트 엔티티
│   │   ├── repository/
│   │   │   └── ReportRepository.java   # 리포트 레포지토리
│   │   └── service/
│   │       ├── ReportService.java      # 리포트 서비스 인터페이스
│   │       └── impl/
│   │           └── ReportServiceImpl.java # 리포트 서비스 구현체
│   │
│   ├── resume/                         # 자소서 모듈
│   │   ├── controller/
│   │   │   └── ResumeController.java   # 자소서 API 엔드포인트
│   │   ├── dto/
│   │   │   ├── ResumeDetailResponseDto.java # 자소서 상세 응답 DTO
│   │   │   ├── ResumeDetailWrapperDto.java # 자소서 상세 래퍼 DTO
│   │   │   ├── ResumeRequestDto.java   # 자소서 요청 DTO
│   │   │   ├── ResumeResponseDto.java  # 자소서 응답 DTO
│   │   │   └── ResumeWithInterviewIdProjection.java # 면접 ID 포함 자소서 프로젝션
│   │   ├── entity/
│   │   │   └── Resume.java             # 자소서 엔티티
│   │   ├── event/
│   │   │   └── [이벤트 관련 파일들]    # 자소서 관련 이벤트
│   │   ├── repository/
│   │   │   └── ResumeRepository.java   # 자소서 레포지토리
│   │   └── service/
│   │       ├── ResumeService.java      # 자소서 서비스 인터페이스
│   │       └── impl/
│   │           └── ResumeServiceImpl.java # 자소서 서비스 구현체
│   │
│   ├── security/                       # 보안 모듈
│   │   ├── handler/                    # Spring Security 핸들러
│   │   │   ├── CustomAccessDeniedHandler.java # 접근 거부 핸들러
│   │   │   └── JwtAuthenticationEntryPoint.java # JWT 인증 진입점 핸들러
│   │   ├── service/                    # 보안 서비스
│   │   │   ├── CustomUserDetailsService.java # Spring Security 사용자 세부정보 서비스
│   │   │   ├── JwtAuthenticationFilter.java # JWT 인증 필터
│   │   │   └── JwtTokenProvider.java   # JWT 토큰 생성/검증 제공자
│   │   └── UserPrincipal.java          # Spring Security 사용자 주체 클래스
│   │
│   ├── user/                           # 사용자 관리 모듈
│   │   ├── controller/
│   │   │   └── UserController.java     # 사용자 관리 API
│   │   ├── dto/                        # 사용자 관련 DTO
│   │   ├── entity/
│   │   │   └── User.java               # 사용자 엔티티
│   │   ├── repository/
│   │   │   └── UserRepository.java     # 사용자 레포지토리
│   │   └── service/
│   │       ├── UserService.java        # 사용자 서비스 인터페이스
│   │       └── impl/
│   │           └── UserServiceImpl.java # 사용자 서비스 구현체
│   │
│   └── watch/                          # 워치 연동 모듈
│       ├── controller/
│       │   └── GalaxyWatchController.java # 갤럭시 워치 API
│       ├── dto/
│       │   ├── GalaxyRequestDto.java   # 워치 요청 DTO
│       │   └── GalaxyResponseDto.java  # 워치 응답 DTO
│       ├── entity/
│       │   └── GalaxyWatch.java        # 워치 엔티티
│       ├── repository/
│       │   └── GalaxyWatchRepository.java # 워치 레포지토리
│       └── service/
│           ├── GalaxyWatchService.java # 워치 서비스 인터페이스
│           └── impl/
│               └── GalaxyWatchServiceImpl.java # 워치 서비스 구현체
│
├── src/main/resources/
│   ├── application.yml                 # 공통 설정
│   ├── application-local.yml           # 로컬 환경 설정
│   └── application-dev.yml             # 개발 환경 설정
│
├── build.gradle                        # Gradle 빌드 설정
├── settings.gradle                     # Gradle 프로젝트 설정
├── Dockerfile                          # Docker 이미지 빌드 파일
├── Jenkinsfile                         # Jenkins CI/CD 파이프라인
├── gradlew / gradlew.bat               # Gradle Wrapper 실행 스크립트
└── gradle/                             # Gradle Wrapper 설정
```

## 기술 스택

### Core Framework

- **Java 17**: 최신 LTS 버전
- **Spring Boot 3.5.7**: 애플리케이션 프레임워크
- **Spring Security**: 인증 및 보안
- **Spring Data JPA**: 데이터 접근 계층
- **Spring WebFlux**: 비동기 웹 클라이언트

### Database

- **MySQL**: 관계형 데이터베이스 (로컬/개발 환경)
- **H2 Database**: 인메모리 데이터베이스 (테스트용)
- **MongoDB**: 문서형 데이터베이스 (심박수 데이터 저장)
- **Redis**: 캐시 및 세션 관리
- **JPA/Hibernate**: ORM 프레임워크

### Security & Authentication

- **JWT (JSON Web Token)**: 토큰 기반 인증
- **JJWT 0.12.5**: JWT 라이브러리
- **BCrypt**: 비밀번호 암호화

### Cloud Services

- **AWS S3**: 파일 저장소 (면접 오디오, 질문 오디오 등)
- **AWS SDK 2.25.66**: AWS 서비스 연동

### Documentation & Monitoring

- **SpringDoc OpenAPI 3**: API 문서화 (Swagger UI)
- **Swagger UI**: API 테스트 및 문서화

### Development Tools

- **Lombok**: 코드 간소화
- **Gradle**: 빌드 도구

### External Services

- **FastAPI**: AI 서비스 연동 (면접 리포트 생성 등)
- **WebClient**: 비동기 HTTP 클라이언트

## 주요 기능

### 1. 인증 및 사용자 관리

- **회원가입**: 새로운 사용자 등록
- **로그인/로그아웃**: JWT 기반 인증
- **토큰 관리**: Access Token & Refresh Token
- **사용자 정보 관리**: 프로필 조회 및 수정, 비밀번호 변경

### 2. 면접 관리

- **면접 생성**: 자소서 기반 면접 생성
- **면접 진행**: VR 면접 질문 제공 및 답변 수집
- **면접 종료**: 면접 종료 및 리포트 생성 요청
- **꼬리 질문**: AI 기반 연관 질문 생성

### 3. 자소서 관리

- **자소서 등록**: 채용 공고 기반 자소서 작성
- **자소서 조회**: 사용자별 자소서 목록 및 상세 조회
- **자소서 수정**: 자소서 내용 수정

### 4. 리포트 생성

- **리포트 생성**: AI 기반 면접 리포트 생성
- **리포트 조회**: 면접 리포트 상세 및 요약 조회
- **Q&A 분석**: 면접 질문 및 답변 분석

### 5. 심박수 모니터링

- **심박수 수집**: VR 면접 중 심박수 데이터 수집
- **심박수 저장**: MongoDB에 심박수 데이터 저장
- **심박수 조회**: 면접별 심박수 데이터 조회

### 6. 워치 연동

- **워치 등록**: 갤럭시 워치 등록
- **워치 조회**: 등록된 워치 정보 조회
- **워치 해제**: 워치 등록 해제

### 7. 보안 시스템

- **JWT 인증**: Stateless 인증 방식
- **Role 기반 접근 제어**: 사용자 권한 관리
- **CORS 설정**: 안전한 크로스 오리진 통신
- **예외 처리**: 체계적인 에러 핸들링

### 8. API 표준화

- **통일된 응답 구조**: 모든 API 동일한 형태
- **상태 코드 체계화**: 성공/에러 코드 분류
- **자동 문서화**: Swagger UI를 통한 API 문서 제공

## 예외 처리 시스템

### 에러 코드 분류

- **400 Bad Request**: 잘못된 요청 파라미터
- **401 Unauthorized**: 인증 실패 (로그인 필요)
- **403 Forbidden**: 권한 부족
- **404 Not Found**: 리소스를 찾을 수 없음
- **409 Conflict**: 데이터 충돌 (중복 등)
- **410 Gone**: 만료된 리소스
- **422 Unprocessable Entity**: 형식은 맞으나 비즈니스 규칙 위반
- **500 Internal Server Error**: 서버 내부 오류
- **502 Bad Gateway**: 업스트림/AI 서비스 연동 실패
- **504 Gateway Timeout**: 업스트림 타임아웃

### API 응답 구조

```json
{
  "success": true,
  "code": 200,
  "message": "요청이 성공적으로 처리되었습니다.",
  "data": {
    // 응답 데이터
  }
}
```

## API 문서

- **Swagger UI**: http://localhost:8080/swagger-ui.html
- **API Docs**: http://localhost:8080/v3/api-docs

## 빌드 및 실행

### 1. 로컬 개발 환경 (Gradle)

#### 사전 요구사항

- Java 17 이상
- MySQL 8.0 이상
- Redis
- MongoDB
- AWS 계정 (S3 사용 시)

#### 환경 변수 설정

프로젝트 루트에 있는 `example.env` 파일을 참고하여 환경 변수를 설정하세요:

```bash
# example.env 파일을 .env로 복사
cp example.env .env

# .env 파일을 편집하여 실제 값으로 수정
# 예: JWT_SECRET, 데이터베이스 비밀번호, AWS 자격 증명 등
```

**필수 환경 변수 목록:**

```bash
# JWT 설정
JWT_SECRET=your-jwt-secret-key-here

# 데이터베이스 설정 (로컬 환경)
MYSQL_DATABASE=your-database-name
MYSQL_USER=your-mysql-user
MYSQL_PASSWORD=your-mysql-password

# 데이터베이스 설정 (개발/프로덕션 환경)
DB_HOST=localhost
DB_PORT=3306
DB_NAME=your-database-name
DB_USER=your-db-user
DB_PASSWORD=your-db-password

# Redis 설정
REDIS_HOST=localhost
REDIS_PORT=6379
REDIS_PASSWORD=

# MongoDB 설정
MONGODB_HOST=localhost
MONGODB_PORT=27017
MONGODB_DATABASE=your-mongodb-database
MONGODB_USERNAME=
MONGODB_PASSWORD=

# AWS S3 설정
AWS_ACCESS_KEY_ID=your-access-key
AWS_SECRET_ACCESS_KEY=your-secret-key
AWS_S3_BUCKET=your-s3-bucket-name

# AI 서비스 설정
AI_FASTAPI_BASE_URL=http://localhost:8000
```

> **주의**: `.env` 파일은 Git에 커밋되지 않도록 `.gitignore`에 포함되어 있습니다. 실제 환경 변수 값은 `example.env`를 참고하여 설정하세요.

#### 프로젝트 빌드

```bash
# 프로젝트 빌드
./gradlew build

# 애플리케이션 실행
./gradlew bootRun
```

#### 프로파일별 실행

```bash
# 로컬 환경 실행
./gradlew bootRun --args='--spring.profiles.active=local'

# 개발 환경 실행
./gradlew bootRun --args='--spring.profiles.active=dev'
```

### 2. Docker 환경

#### 사전 요구사항

- Docker
- Docker Compose

#### Docker 이미지 빌드

```bash
# JAR 파일 빌드
./gradlew bootJar

# Docker 이미지 빌드 (Dockerfile 사용)
docker build -t mindstage-backend:latest .
```

#### Docker Compose 실행

```bash
# 서비스 실행 (백그라운드)
docker-compose up -d

# 로그 확인
docker-compose logs -f

# 서비스 상태 확인
docker-compose ps

# 서비스 중지
docker-compose stop

# 서비스 중지 및 컨테이너 제거
docker-compose down

# 볼륨까지 모두 제거 (데이터 삭제 주의!)
docker-compose down -v
```

### 3. 서비스 접속 정보

#### 개발 환경 (로컬)

- **애플리케이션**: http://localhost:8080
- **Swagger UI**: http://localhost:8080/swagger-ui.html
- **API Docs**: http://localhost:8080/v3/api-docs

#### 데이터베이스 연결

- **MySQL**: localhost:3306
- **Redis**: localhost:6379
- **MongoDB**: localhost:27017

### 4. 환경별 설정

#### 로컬 환경 (application-local.yml)

- MySQL 데이터베이스 사용
- 로컬 Redis 연결
- 로컬 MongoDB 연결
- 개발용 로깅 설정

#### 개발 환경 (application-dev.yml)

- 환경 변수 기반 데이터베이스 설정
- 외부 Redis 연결
- 외부 MongoDB 연결
- 파일 로깅 설정

#### 프로덕션 환경 (application-prod.yml)

- 프로덕션 데이터베이스 설정
- 프로덕션 Redis 연결
- 프로덕션 MongoDB 연결
- 프로덕션 로깅 설정

## CI/CD

### Jenkins 파이프라인

프로젝트는 Jenkins를 사용한 CI/CD 파이프라인을 지원합니다.

#### 파이프라인 단계

1. **변경 감지**: `be/` 디렉토리 변경 감지
2. **Gradle 빌드**: `bootJar` 태스크 실행
3. **Docker 빌드**: Docker Compose를 통한 이미지 빌드
4. **배포**: 컨테이너 배포

#### Jenkinsfile 위치

- `Jenkinsfile`: 프로젝트 루트에 위치

## 트러블슈팅

### 포트 충돌 해결

```bash
# 사용 중인 포트 확인
netstat -ano | findstr :8080

# 포트 변경이 필요한 경우 application.yml에서 server.port 수정
```

### 데이터베이스 연결 오류

```bash
# MySQL 연결 확인
mysql -u root -p -h localhost

# Redis 연결 확인
redis-cli ping

# MongoDB 연결 확인
mongosh
```

### Docker 빌드 캐시 문제

```bash
# 캐시 없이 다시 빌드
docker build --no-cache -t mindstage-backend:latest .

# Docker 시스템 정리
docker system prune -f
```

### 환경 변수 설정 오류

- 환경 변수가 제대로 설정되었는지 확인
- `application.yml`에서 환경 변수 참조 방식 확인
- Docker 환경에서는 `docker-compose.yml`에서 환경 변수 설정 확인

## 라이선스

이 프로젝트는 SSAFY 프로젝트입니다.

## 기여

프로젝트 기여 시 다음 사항을 준수해주세요:

1. 코드 스타일 가이드 준수
2. 테스트 코드 작성
3. 커밋 메시지 작성 규칙 준수
4. Pull Request 작성 시 상세한 설명 포함

## 문의

프로젝트 관련 문의사항이 있으시면 이슈를 등록해주세요.

