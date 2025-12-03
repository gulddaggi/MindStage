package com.ssafy.s13p21b204.global.fastapi;

import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.fastapi.dto.AiEndInterviewInput;
import com.ssafy.s13p21b204.global.fastapi.dto.AiEndInterviewResponse;
import com.ssafy.s13p21b204.global.fastapi.dto.AiInterviewInput;
import com.ssafy.s13p21b204.global.fastapi.dto.AiInterviewResponse;
import com.ssafy.s13p21b204.global.fastapi.dto.AiOCRInput;
import com.ssafy.s13p21b204.global.fastapi.dto.AiSttRequestDto;
import com.ssafy.s13p21b204.global.fastapi.dto.AiSttResponseDto;
import com.ssafy.s13p21b204.global.fastapi.dto.AiTextResponse;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.http.HttpStatus;
import org.springframework.http.HttpStatusCode;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.web.reactive.function.client.WebClient;
import org.springframework.web.reactive.function.client.WebClientResponseException;
import reactor.core.publisher.Mono;
import reactor.util.retry.Retry;

import java.time.Duration;

@Component
@RequiredArgsConstructor
@Slf4j
public class AiClient {

    private final WebClient fastApiWebClient;
    private final ObjectMapper objectMapper;

    /**
     * OCR API 호출 (우대사항 파일 텍스트 추출)
     */
    public AiTextResponse performOcr(String preSignedUrl) {
        AiOCRInput body = AiOCRInput.builder()
            .preSignedUrl(preSignedUrl)
            .build();

        return postJson("/api/v1/ocr", body, AiTextResponse.class)
            .block();  // 동기 처리
    }

    /**
     * 면접 시작 API 호출
     */
    public AiInterviewResponse startInterview(AiInterviewInput input) {
        // 요청 바디 로깅 (JSON)
        try {
            String requestJson = objectMapper.writeValueAsString(input);
            log.info("[AiClient] /api/v1/interview/start 요청 바디: {}", requestJson);
        } catch (Exception e) {
            log.warn("[AiClient] /api/v1/interview/start 요청 바디 직렬화 실패: {}", e.getMessage());
        }
        return postJson("/api/v1/interview/start", input, AiInterviewResponse.class)
            .block();  // 동기 처리
    }

    /**
     * 면접 답변 API 호출
     */
    public AiInterviewResponse answerInterview(AiInterviewInput input) {
        // 요청 바디 로깅 (JSON)
        try {
            String requestJson = objectMapper.writeValueAsString(input);
            log.info("[AiClient] /api/v1/interview/answer 요청 바디: {}", requestJson);
        } catch (Exception e) {
            log.warn("[AiClient] /api/v1/interview/answer 요청 바디 직렬화 실패: {}", e.getMessage());
        }
        return postJson("/api/v1/interview/answer", input, AiInterviewResponse.class)
            .block();  // 동기 처리
    }


    /**
     * 면접 종료 API 호출
     */
    public AiEndInterviewResponse endInterview(AiEndInterviewInput input) {
        // 요청 바디 로깅 (JSON)
        try {
            String requestJson = objectMapper.writeValueAsString(input);
            log.info("[AiClient] /api/v1/interview/end 요청 바디: {}", requestJson);
        } catch (Exception e) {
            log.warn("[AiClient] /api/v1/interview/end 요청 바디 직렬화 실패: {}", e.getMessage());
        }
        return postJson("/api/v1/interview/end", input, AiEndInterviewResponse.class)
            .block();  // 동기 처리
    }

    /**
     * STT API 호출 (음성 파일 텍스트 변환)
     */
    public String transcribeAudio(String sttUrl) {
        AiSttRequestDto request = new AiSttRequestDto(sttUrl);
        
        try {
            String requestJson = objectMapper.writeValueAsString(request);
            log.info("[AiClient] /api/v1/stt 요청 바디: {}", requestJson);
        } catch (Exception e) {
            log.warn("[AiClient] /api/v1/stt 요청 바디 직렬화 실패: {}", e.getMessage());
        }
        
        AiSttResponseDto response = postJson("/api/v1/stt", request, AiSttResponseDto.class)
            .block();  // 동기 처리
        
        // 응답 검증
        if (response == null || response.convertedText() == null) {
            log.error("[AiClient] STT 응답이 올바르지 않음 - response: {}", response);
            throw ApiException.of(HttpStatus.INTERNAL_SERVER_ERROR, 
                ErrorMessage.INTERNAL_SERVER_ERROR);
        }
        
        if (!"success".equals(response.status())) {
            log.warn("[AiClient] STT 처리 실패 - status: {}", response.status());
            throw ApiException.of(HttpStatus.BAD_GATEWAY, 
                "STT 변환에 실패했습니다. status: " + response.status());
        }
        
        log.info("[AiClient] STT 변환 완료 - 변환된 텍스트 길이: {}", response.convertedText().length());
        return response.convertedText();
    }

    /**
     * WebClient 기반 POST 요청 (비동기 - 동기 변환)
     * - 재시도 정책: 최대 3번, 500 에러 및 타임아웃 시
     * - 폴백 정책: 제거됨 (명확한 실패 처리를 위해)
     */
    private <T> Mono<T> postJson(String path, Object body, Class<T> responseType) {
        return fastApiWebClient.post()
            .uri(path)
            .contentType(MediaType.APPLICATION_JSON)
            .accept(MediaType.APPLICATION_JSON)
            .bodyValue(body)
            .retrieve()
            .onStatus(
                HttpStatusCode::is4xxClientError,
                clientResponse -> clientResponse.bodyToMono(String.class)
                    .flatMap(errorBody -> {
                        log.warn("[AiClient] {} 4xx 에러: status={}, body={}", 
                            path, clientResponse.statusCode(), errorBody);
                        return Mono.error(ApiException.of(
                            HttpStatus.valueOf(clientResponse.statusCode().value()),
                            errorBody.isBlank() ? ErrorMessage.BAD_REQUEST : errorBody
                        ));
                    })
            )
            .onStatus(
                HttpStatusCode::is5xxServerError,
                clientResponse -> clientResponse.bodyToMono(String.class)
                    .flatMap(errorBody -> {
                        log.warn("[AiClient] {} 5xx 에러: status={}, body={}", 
                            path, clientResponse.statusCode(), errorBody);
                        return Mono.error(new WebClientResponseException(
                            clientResponse.statusCode().value(),
                            "AI 서버 에러",
                            clientResponse.headers().asHttpHeaders(),
                            errorBody.getBytes(),
                            null
                        ));
                    })
            )
            .bodyToMono(responseType)
            // 재시도 정책: 최대 3번 재시도, 지수 백오프
            .retryWhen(Retry.backoff(3, Duration.ofSeconds(1))
                .filter(throwable -> {
                    // 5xx 에러 또는 타임아웃 시 재시도
                    if (throwable instanceof WebClientResponseException wcre) {
                        int statusCode = wcre.getStatusCode().value();
                        boolean shouldRetry = statusCode >= 500 && statusCode < 600;
                        if (shouldRetry) {
                            log.warn("[AiClient] {} 재시도 예정 - status={}", path, statusCode);
                        }
                        return shouldRetry;
                    }
                    // 타임아웃, 커넥션 에러 시 재시도
                    boolean isNetworkError = throwable instanceof java.net.ConnectException
                        || throwable instanceof java.util.concurrent.TimeoutException
                        || throwable.getCause() instanceof java.net.SocketTimeoutException;
                    
                    if (isNetworkError) {
                        log.warn("[AiClient] {} 네트워크 에러 - 재시도 예정: {}", path, throwable.getMessage());
                    }
                    return isNetworkError;
                })
                .doBeforeRetry(retrySignal -> 
                    log.info("[AiClient] {} 재시도 {}/3", path, retrySignal.totalRetries() + 1)
                )
            )
            // 폴백 정책: 재시도 실패 시 처리
            // 폴백 제거: 명확한 실패 처리를 위해 폴백 응답을 반환하지 않음
            // 폴백이 있어도 결국 질문 개수 불일치로 실패 처리되므로 의미 없음
            .onErrorResume(WebClientResponseException.class, e -> {
                log.error("[AiClient] {} 최종 실패 (WebClient 에러): status={}, body={}", 
                    path, e.getStatusCode(), e.getResponseBodyAsString());
                
                // 폴백 제거 - 명확한 예외 발생
                // 기존 폴백 코드 (주석 처리):
                // if (path.contains("/interview/start") && responseType == AiInterviewResponse.class) {
                //     log.warn("[AiClient] {} 폴백: 기본 응답 반환", path);
                //     return Mono.just(createDefaultInterviewResponse()).cast(responseType);
                // }
                
                return Mono.error(ApiException.of(
                    HttpStatus.valueOf(e.getStatusCode().value()),
                    e.getResponseBodyAsString()
                ));
            })
            .onErrorResume(ApiException.class, Mono::error)  // ApiException은 그대로 전파
            .onErrorResume(throwable -> {
                log.error("[AiClient] {} 최종 실패 (기타 에러): {}", path, throwable.getMessage(), throwable);
                
                // 타임아웃 에러
                if (throwable instanceof java.util.concurrent.TimeoutException
                    || throwable.getCause() instanceof java.net.SocketTimeoutException) {
                    return Mono.error(ApiException.of(
                        HttpStatus.GATEWAY_TIMEOUT,
                        ErrorMessage.AI_SERVICE_TIMEOUT
                    ));
                }
                
                // 연결 에러
                if (throwable instanceof java.net.ConnectException
                    || throwable.getCause() instanceof java.net.UnknownHostException) {
                    return Mono.error(ApiException.of(
                        HttpStatus.SERVICE_UNAVAILABLE,
                        ErrorMessage.AI_SERVICE_CONNECTION_ERROR
                    ));
                }
                
                // 기타 에러
                return Mono.error(ApiException.of(
                    HttpStatus.INTERNAL_SERVER_ERROR,
                    ErrorMessage.INTERNAL_SERVER_ERROR
                ));
            })
            .doOnSuccess(response -> 
                log.info("[AiClient] {} 요청 성공", path)
            );
    }

    /**
     * 폴백 응답 생성 (주석 처리 - 폴백 정책 제거)
     * 폴백이 있어도 결국 질문 개수 불일치로 실패 처리되므로 의미 없음
     */
    // private AiInterviewResponse createDefaultInterviewResponse() {
    //     return new AiInterviewResponse(
    //         "fallback",
    //         null,
    //         Collections.singletonList("면접 시스템이 일시적으로 사용 불가합니다. 잠시 후 다시 시도해주세요."),
    //         Collections.singletonList(0)
    //     );
    // }
}
