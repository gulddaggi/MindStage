# PowerShell 버전 - 갤럭시 워치 심박수 데이터 전송 시뮬레이터

param(
    [int]$InterviewId = 1,
    [string]$DeviceUuid = "550e8400-e29b-41d4-a716-446655440000",
    [int]$DurationMinutes = 15
)

$BaseUrl = if ($env:API_URL) { $env:API_URL } else { "http://localhost:8080" }
$JwtToken = if ($env:JWT_TOKEN) { $env:JWT_TOKEN } else { "your-jwt-token-here" }

Write-Host "==================================================" -ForegroundColor Blue
Write-Host "    갤럭시 워치 심박수 데이터 전송 시뮬레이터" -ForegroundColor Blue
Write-Host "==================================================" -ForegroundColor Blue
Write-Host "면접 ID: $InterviewId" -ForegroundColor Green
Write-Host "디바이스 UUID: $DeviceUuid" -ForegroundColor Green
Write-Host "면접 시간: $DurationMinutes 분" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Blue
Write-Host ""

$TotalPoints = $DurationMinutes * 60
Write-Host "총 $TotalPoints 개의 심박수 데이터 생성 중..." -ForegroundColor Yellow
Write-Host ""

# 시작 시간 (UTC)
$StartTime = (Get-Date).AddMinutes(-$DurationMinutes).ToUniversalTime()

# JSON 데이터 생성
$DataPoints = @()

for ($i = 0; $i -lt $TotalPoints; $i++) {
    $Timestamp = $StartTime.AddSeconds($i).ToString("yyyy-MM-ddTHH:mm:ss")
    
    # 심박수 패턴 생성
    $Progress = ($i * 100) / $TotalPoints
    
    if ($Progress -lt 10) {
        $BaseBpm = 80
        $Range = 10
    } elseif ($Progress -lt 70) {
        $BaseBpm = 70
        $Range = 5
    } else {
        $BaseBpm = 85
        $Range = 10
    }
    
    $RandomOffset = Get-Random -Minimum (-$Range) -Maximum $Range
    $Bpm = $BaseBpm + $RandomOffset
    
    $DataPoints += @{
        bpm = $Bpm
        measuredAt = $Timestamp
    }
    
    # 진행률 표시 (10%마다)
    if ($i % [Math]::Floor($TotalPoints / 10) -eq 0) {
        Write-Host "생성 진행률: $([Math]::Floor($Progress))%" -ForegroundColor Green
    }
}

$JsonBody = @{
    interviewId = $InterviewId
    deviceUuid = $DeviceUuid
    dataPoints = $DataPoints
} | ConvertTo-Json -Depth 10

Write-Host ""
Write-Host "데이터 생성 완료!" -ForegroundColor Green
Write-Host "서버로 전송 중..." -ForegroundColor Yellow
Write-Host ""

# API 호출
try {
    $Headers = @{
        "Content-Type" = "application/json"
        "Authorization" = "Bearer $JwtToken"
    }
    
    $Response = Invoke-WebRequest -Uri "$BaseUrl/api/heartbeat/batch" `
        -Method POST `
        -Headers $Headers `
        -Body $JsonBody `
        -UseBasicParsing
    
    Write-Host "==================================================" -ForegroundColor Blue
    Write-Host "✓ 전송 성공! (HTTP $($Response.StatusCode))" -ForegroundColor Green
    Write-Host $Response.Content -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Blue
} catch {
    Write-Host "==================================================" -ForegroundColor Blue
    Write-Host "✗ 전송 실패! (HTTP $($_.Exception.Response.StatusCode.value__))" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host "==================================================" -ForegroundColor Blue
}

