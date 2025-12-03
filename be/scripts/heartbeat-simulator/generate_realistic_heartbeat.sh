#!/bin/bash

# 색상 정의
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

BASE_URL="${API_URL:-http://localhost:8080}"
JWT_TOKEN="${JWT_TOKEN:-your-jwt-token-here}"

INTERVIEW_ID="${1:-1}"
DEVICE_UUID="${2:-550e8400-e29b-41d4-a716-446655440000}"

echo -e "${BLUE}==================================================${NC}"
echo -e "${BLUE}  현실적인 면접 심박수 패턴 시뮬레이터${NC}"
echo -e "${BLUE}==================================================${NC}"

# 면접 시나리오 정의
# 0-2분: 대기 및 시작 (긴장, 70-90 BPM)
# 2-5분: 자기소개 (안정화, 65-75 BPM)
# 5-10분: 기술 질문 (집중, 70-85 BPM)
# 10-15분: 압박 질문 (긴장 상승, 80-100 BPM)
# 15-18분: 마무리 (안정, 70-80 BPM)

SCENARIOS=(
  "0:120:70:20:대기_및_인사"
  "120:300:65:10:자기소개"
  "300:600:75:15:기술질문"
  "600:900:85:15:압박질문"
  "900:1080:70:10:마무리"
)

START_TIME=$(date -u -d "18 minutes ago" +"%Y-%m-%dT%H:%M:%S")

JSON_DATA="{
  \"interviewId\": ${INTERVIEW_ID},
  \"deviceUuid\": \"${DEVICE_UUID}\",
  \"dataPoints\": ["

FIRST_POINT=true

for scenario in "${SCENARIOS[@]}"; do
  IFS=':' read -r START_SEC END_SEC BASE_BPM RANGE PHASE <<< "$scenario"
  echo -e "${YELLOW}시나리오: ${PHASE} (${BASE_BPM}±${RANGE} BPM)${NC}"
  
  for ((i=START_SEC; i<END_SEC; i++)); do
    if [[ "$OSTYPE" == "darwin"* ]]; then
      TIMESTAMP=$(date -u -j -f "%Y-%m-%dT%H:%M:%S" "${START_TIME}" -v+${i}S +"%Y-%m-%dT%H:%M:%S")
    else
      TIMESTAMP=$(date -u -d "${START_TIME} ${i} seconds" +"%Y-%m-%dT%H:%M:%S")
    fi
    
    # 랜덤 변동
    RANDOM_OFFSET=$((RANDOM % (RANGE * 2) - RANGE))
    BPM=$((BASE_BPM + RANDOM_OFFSET))
    
    # 첫 포인트가 아니면 쉼표 추가
    if [ "$FIRST_POINT" = false ]; then
      JSON_DATA+=","
    fi
    FIRST_POINT=false
    
    JSON_DATA+="
    {
      \"bpm\": ${BPM},
      \"measuredAt\": \"${TIMESTAMP}\"
    }"
  done
done

JSON_DATA+="
  ]
}"

echo -e "\n${GREEN}데이터 생성 완료! 서버로 전송 중...${NC}\n"

RESPONSE=$(curl -s -w "\n%{http_code}" -X POST \
  "${BASE_URL}/api/heartbeat/batch" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${JWT_TOKEN}" \
  -d "${JSON_DATA}")

HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
RESPONSE_BODY=$(echo "$RESPONSE" | sed '$d')

echo -e "${BLUE}==================================================${NC}"
if [ "$HTTP_CODE" -eq 200 ]; then
  echo -e "${GREEN}✓ 전송 성공! (HTTP ${HTTP_CODE})${NC}"
  echo -e "${GREEN}${RESPONSE_BODY}${NC}"
else
  echo -e "${RED}✗ 전송 실패! (HTTP ${HTTP_CODE})${NC}"
  echo -e "${RED}${RESPONSE_BODY}${NC}"
fi
echo -e "${BLUE}==================================================${NC}"

