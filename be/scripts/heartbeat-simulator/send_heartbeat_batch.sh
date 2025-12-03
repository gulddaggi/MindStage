#!/bin/bash

# 색상 정의
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 서버 설정
BASE_URL="${API_URL:-http://localhost:8080}"
JWT_TOKEN="${JWT_TOKEN:-your-jwt-token-here}"

# 면접 정보 (스크립트 실행 시 인자로 받거나 기본값 사용)
INTERVIEW_ID="${1:-1}"
DEVICE_UUID="${2:-550e8400-e29b-41d4-a716-446655440000}"
DURATION_MINUTES="${3:-15}"

echo -e "${BLUE}==================================================${NC}"
echo -e "${BLUE}    갤럭시 워치 심박수 데이터 전송 시뮬레이터${NC}"
echo -e "${BLUE}==================================================${NC}"
echo -e "${GREEN}면접 ID: ${INTERVIEW_ID}${NC}"
echo -e "${GREEN}디바이스 UUID: ${DEVICE_UUID}${NC}"
echo -e "${GREEN}면접 시간: ${DURATION_MINUTES}분${NC}"
echo -e "${BLUE}==================================================${NC}\n"

# 데이터 포인트 생성 (1초마다 측정 - 15분이면 900개)
TOTAL_POINTS=$((DURATION_MINUTES * 60))
echo -e "${YELLOW}총 ${TOTAL_POINTS}개의 심박수 데이터 생성 중...${NC}\n"

# 현재 시간 기준으로 시작 시간 설정 (15분 전)
START_TIME=$(date -u -d "${DURATION_MINUTES} minutes ago" +"%Y-%m-%dT%H:%M:%S")

# JSON 데이터 생성 시작
JSON_DATA="{
  \"interviewId\": ${INTERVIEW_ID},
  \"deviceUuid\": \"${DEVICE_UUID}\",
  \"dataPoints\": ["

# 심박수 데이터 생성 (60-100 BPM 사이 랜덤, 면접 긴장도에 따라 변화)
for ((i=0; i<TOTAL_POINTS; i++)); do
  # 시간 계산 (시작 시간 + i초)
  if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    TIMESTAMP=$(date -u -j -f "%Y-%m-%dT%H:%M:%S" "${START_TIME}" -v+${i}S +"%Y-%m-%dT%H:%M:%S")
  else
    # Linux
    TIMESTAMP=$(date -u -d "${START_TIME} ${i} seconds" +"%Y-%m-%dT%H:%M:%S")
  fi
  
  # 면접 시작 시 긴장 -> 중반 안정 -> 후반 다시 상승하는 패턴
  PROGRESS=$((i * 100 / TOTAL_POINTS))
  
  if [ $PROGRESS -lt 10 ]; then
    # 시작: 70-90 (긴장)
    BASE_BPM=80
    RANGE=10
  elif [ $PROGRESS -lt 70 ]; then
    # 중반: 65-75 (안정)
    BASE_BPM=70
    RANGE=5
  else
    # 후반: 75-95 (다시 긴장)
    BASE_BPM=85
    RANGE=10
  fi
  
  # 랜덤 심박수 생성
  RANDOM_OFFSET=$((RANDOM % (RANGE * 2) - RANGE))
  BPM=$((BASE_BPM + RANDOM_OFFSET))
  
  # JSON 데이터 포인트 추가
  JSON_DATA+="
    {
      \"bpm\": ${BPM},
      \"measuredAt\": \"${TIMESTAMP}\"
    }"
  
  # 마지막 항목이 아니면 쉼표 추가
  if [ $i -lt $((TOTAL_POINTS - 1)) ]; then
    JSON_DATA+=","
  fi
  
  # 진행률 표시 (10%마다)
  if [ $((i % (TOTAL_POINTS / 10))) -eq 0 ]; then
    echo -e "${GREEN}생성 진행률: ${PROGRESS}%${NC}"
  fi
done

# JSON 데이터 완성
JSON_DATA+="
  ]
}"

echo -e "\n${GREEN}데이터 생성 완료!${NC}"
echo -e "${YELLOW}서버로 전송 중...${NC}\n"

# API 호출
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST \
  "${BASE_URL}/api/heartbeat/batch" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${JWT_TOKEN}" \
  -d "${JSON_DATA}")

# HTTP 상태 코드 추출
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
RESPONSE_BODY=$(echo "$RESPONSE" | sed '$d')

# 결과 출력
echo -e "${BLUE}==================================================${NC}"
if [ "$HTTP_CODE" -eq 200 ]; then
  echo -e "${GREEN}✓ 전송 성공! (HTTP ${HTTP_CODE})${NC}"
  echo -e "${GREEN}${RESPONSE_BODY}${NC}"
else
  echo -e "${RED}✗ 전송 실패! (HTTP ${HTTP_CODE})${NC}"
  echo -e "${RED}${RESPONSE_BODY}${NC}"
fi
echo -e "${BLUE}==================================================${NC}"

