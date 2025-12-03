# 팀 프로젝트 컨벤션 문서

## 개요

본 문서는 본 프로젝트의 협업 규칙(코드 스타일, 브랜치 전략, 레포지토리 구조, 이슈/Jira, 배포 정책)을 정의합니다. 모든 구성원은 문서를 준수하며, 변경은 PR로 제안/리뷰 후 반영합니다.

---

## 레포지토리 구조

**모노레포(단일 저장소)** 전략을 사용합니다.

- 하나의 레포지토리에서 프론트엔드, 백엔드, 공통 패키지, 인프라 코드를 함께 관리합니다.

```
root/
├── vr/                 # Unity 2022.x LTS
│   ├── Assets/
│   ├── ProjectSettings/
│   └── Packages/
├── wear/               # Kotlin (Wear OS)
│   ├── app/
│   └── build.gradle.kts
├── be/                 # Spring Boot (Gradle)
│   ├── src/
│   ├── build.gradle.kts
│   └── application.example.yml  # 실제 yml은 커밋 금지, 예시 유지
├── ai/                 # Python (AI/모델/노트북)
│   ├── src/
│   ├── notebooks/
│   └── requirements.txt or pyproject.toml
├── infra/              # IaC/CI-CD 스크립트/배포 템플릿
│   └── scripts/
├── docs/               # 설계/아키텍처/회의록
└── CONVENTION.md
```

> 설정/시크릿: `.env*`, `be/**/application*.yml`은 커밋 금지. `application.example.yml`만 커밋.

---

## Git 브랜치 전략 (git-flow)

- `main` : 운영 배포 브랜치
- `develop` : 다음 릴리스 준비 브랜치
- `feature/` : 기능 개발 (예: `feature/vr-input-remap`)
- `release/` : 릴리스 준비 (예: `release/1.0.0`)
- `hotfix/` : 운영 긴급 수정 (예: `hotfix/1.0.1`)

---

## Merge 정책

| From → To         | 방식               | 비고                      |
| ----------------- | ------------------ | ------------------------- |
| feature → develop | **Squash & Merge** | 기능 단위로 커밋 정리     |
| develop → main    | **Rebase & Merge** | 배포 시점 히스토리 선형화 |
| release → main    | **Merge Commit**   | 배포 단위 구분            |
| hotfix → main     | **Merge Commit**   | 긴급 패치 구분            |
| hotfix → develop  | **Merge Commit**   | develop에 역반영          |

**PR 필수 체크**

- 리뷰어 1+ 승인, 빌드/테스트 통과, 커밋 메시지/스코프 규칙 준수, Jira 이슈 링크.

---

## 커밋 컨벤션

### 형식

```
type(scope): subject(한글, 명사형)

body (선택, 한글)
```

### 주요 type 키워드

- feat: 새로운 기능 추가
- fix: 버그 수정
- docs: 문서 수정
- style: 코드 스타일 변경 (포매팅, 세미콜론 등)
- refactor: 리팩토링 (기능 변경 없음)
- perf: 성능 개선
- test: 테스트 코드 추가/수정
- chore: 빌드/설정 관련 작업
- ci: CI/CD 설정 변경
- build: 빌드 시스템 변경

### 주요 scope 키워드

- vr: 유니티 vr 관련 작업
- backend: Spring Boot 관련 작업
- api: 공통 API 또는 REST 인터페이스
- auth: 인증/인가 기능
- db: 데이터베이스, 마이그레이션 작업
- utils: 공통 유틸리티
- infra: AWS 및 IaC 관련 작업
- deps: 의존성 관리
- docs: 문서 관련 작업
- config : 프로젝트 설정, 세팅 관련 작업

### 예시

```
feat(vr): 회원가입 페이지 UI 추가
fix(backend): 로그인 예외 처리 개선
refactor(api): 응답 포맷 통일
chore(db): users 테이블 index 추가
ci(infra): S3 배포 워크플로우 설정
docs(convention): 커밋 컨벤션 문서 작성
```

---

## 코드 스타일 가이드

### Unity (C# / VR)

- **IDE**: Visual Studio
- **버전**: Unity 2023.x LTS
- **네이밍**: `PascalCase`(클래스/메서드), `camelCase`(필드/지역), 상수 `UPPER_SNAKE_CASE`
- **폴더 규칙**: `Scripts/`, `Prefabs/`, `Scenes/`, `Materials/`, `Art/`, `Animations/`
- **씬/프리팹**: 접두어로 모듈 구분 예) `VR_`, `UI_`
- **입력**: OpenXR Action 기반, 하드코딩 금지(매핑 파일/스크립터블 오브젝트화)
- **성능**: GC 최소화(Struct, pooling), Update 남용 금지, 프로파일러/Frame Debugger로 검증

### Wear (Kotlin / Wear OS)

- **언어/빌드**: Kotlin + Gradle(KTS)
- **아키텍처**: MVVM + Jetpack(Compose 권장), 코루틴/Flow
- **스타일**: Kotlin 공식 스타일, ktlint/detekt 적용
- **DI**: Hilt/Koin 중 택1
- **테스트**: Unit/UI 테스트 필수, CI에서 실행

### Backend (Spring Boot / Gradle)

- **언어**: Java 17+
- 패키지 구조: 도메인 중심 설계
- 네이밍: Controller, Service, Repository Layer 분리
- DTO 네이밍: ~Request, ~Response 접미사 사용
- RESTful API 설계 준수
- 예외 처리: GlobalExceptionHandler 사용
- 공통 응답 포맷 예시:
  ```
  json
  {
  "status": "success",
  "data": {},
  "message": "요청이 성공했습니다."
  }
  ```

---

## 구성/시크릿 관리

- **금지 커밋**: `.env*`, `be/**/application*.yml`, 개인 키/서명키(`*.jks`, `*.keystore`)
- **예시 유지**: `application.example.yml`, `.env.example`
- **비밀 저장소**: 클라우드 시크릿(SSM/Secrets Manager 등) 또는 CI 변수 사용

---

## 이슈 및 작업 관리 (Jira)

### 컴포넌트(Component) **필수 지정**

- 사용 목록(고정): **VR, Infra, BE, AI**
- 모든 티켓 생성 시 **Component 미지정 금지**(필수 필드)

### 타입 & 제목 규칙

- 모든 이슈(Epic/Story/Sub-task)의 **제목에 컴포넌트를 접두어로 명시**합니다.
- 형식:
  - **Epic**: `[컴포넌트] <핵심 목표>` 예) `[VR] OpenXR 입력 시스템 리뉴얼`
  - **Story**: `[컴포넌트] <한 줄 목표>` 예) `[BE] 회원가입 예외 처리 개선`
  - **Sub-task**: `[컴포넌트] <구체 작업>` 예) `[AI] 모델 추론 패키징 스크립트 작성`
  - **Bug 표기(선택)**: `[컴포넌트][Bug] <요약>` 예) `[Infra][Bug] 스테이징 배포 실패`
- 본문 형식은 팀 자율. 개요/Acceptance Criteria/참고 링크를 자유롭게 기술합니다.

### 이슈 (GitLab Issues)

- 제목: 간결하고 명확하게 작성 (예: [VR] 로그인 페이지 UI 버그)
- 본문:
  - 개요: 어떤 문제가 발생했는지, 또는 어떤 기능을 추가할 것인지
  - Task list: 체크 박스 `- [ ]` 활용하여 진행(예정) 사항 목록 명시
- 라벨: bug, feature, enhancement, question, documentation 등
- Assignee: 담당자 지정
- 예시 이슈 제목: `[VR] 회원가입 시 중복 이메일 예외 처리 필요`

### 마일스톤 (Milestone)

- 목표 단위로 관리 (예: MVP 출시, v1.0, v1.1 버전 릴리즈)
- 마감 기한 설정
- 포함 이슈: 해당 릴리즈/목표에 포함되는 기능과 버그 이슈를 연결

### Merge Request(MR)

- 제목: `type(scope): 내용`

```
feat(vr): 회원가입 페이지 UI 추가
```

- 본문:
  - 작업 개요
  - 관련 이슈 번호: `#이슈번호` 형태로 작성
  - 작업 상세 내용 및 구현 방식
- MR merge 조건:
  - 최소 1명 이상의 리뷰 승인 필수
  - 커밋 컨벤션, 코딩 스타일 준수 여부 확인
  - CI/CD 도입 시 빌드 및 테스트 통과 여부 확인
  - PR 승인 후 MR 브랜치에 해당되는 방식으로 Merge

---

## PR/MR 가이드

- 템플릿(권장):

```md
## Summary

(무엇을, 왜)

---

## 문서 관리

- 본 문서는 레포 루트 `CONVENTION.md`로 관리, 변경은 PR로 제안
- 설계/결정 기록은 `docs/ADR-YYYYMMDD-<slug>.md` 권장

---

## 버전 기록

- v1.0 (2025-10-24): 최초 작성

---
```
