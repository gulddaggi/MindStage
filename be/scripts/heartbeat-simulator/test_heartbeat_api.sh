#!/bin/bash

# 간단한 테스트용 스크립트 (50개 데이터만)
BASE_URL="${API_URL:-http://localhost:8080}"
JWT_TOKEN="${JWT_TOKEN:-your-jwt-token-here}"
INTERVIEW_ID="${1:-1}"

echo "======================================"
echo "  심박수 API 테스트 스크립트"
echo "======================================"
echo "면접 ID: ${INTERVIEW_ID}"
echo "데이터 개수: 50개 (1분치)"
echo "======================================"
echo ""

START_TIME=$(date -u -d "1 minutes ago" +"%Y-%m-%dT%H:%M:%S")

JSON_DATA="{
  \"interviewId\": ${INTERVIEW_ID},
  \"deviceUuid\": \"test-device-uuid-001\",
  \"dataPoints\": ["

for ((i=0; i<50; i++)); do
  if [[ "$OSTYPE" == "darwin"* ]]; then
    TIMESTAMP=$(date -u -j -f "%Y-%m-%dT%H:%M:%S" "${START_TIME}" -v+${i}S +"%Y-%m-%dT%H:%M:%S")
  else
    TIMESTAMP=$(date -u -d "${START_TIME} ${i} seconds" +"%Y-%m-%dT%H:%M:%S")
  fi
  
  BPM=$((70 + RANDOM % 20))
  
  JSON_DATA+="
    {\"bpm\": ${BPM}, \"measuredAt\": \"${TIMESTAMP}\"}"
  
  [ $i -lt 49 ] && JSON_DATA+=","
done

JSON_DATA+="
  ]
}"

echo "서버로 전송 중..."
echo ""

RESPONSE=$(curl -s -w "\n%{http_code}" -X POST \
  "${BASE_URL}/api/heartbeat/batch" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${JWT_TOKEN}" \
  -d "${JSON_DATA}")

HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
RESPONSE_BODY=$(echo "$RESPONSE" | sed '$d')

if [ "$HTTP_CODE" -eq 200 ]; then
  echo "✓ 테스트 성공! (HTTP ${HTTP_CODE})"
  echo "${RESPONSE_BODY}"
else
  echo "✗ 테스트 실패! (HTTP ${HTTP_CODE})"
  echo "${RESPONSE_BODY}"
fi

echo ""
echo "======================================"
echo "테스트 완료!"
echo "======================================"

