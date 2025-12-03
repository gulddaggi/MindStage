package com.ssafy.s13p21b204.global.config;

import io.netty.channel.ChannelOption;
import io.netty.handler.timeout.ReadTimeoutHandler;
import io.netty.handler.timeout.WriteTimeoutHandler;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.client.reactive.ReactorClientHttpConnector;
import org.springframework.web.reactive.function.client.ExchangeFilterFunction;
import org.springframework.web.reactive.function.client.WebClient;
import reactor.core.publisher.Mono;
import reactor.netty.http.client.HttpClient;
import reactor.netty.resources.ConnectionProvider;

import java.time.Duration;
import java.util.concurrent.TimeUnit;

@Configuration
@Slf4j
public class WebClientConfig {

    // 환경변수 AI_FASTAPI_BASE_URL > application.* (ai.fastapi.base-url) > 기본값 순서로 적용
    @Value("${AI_FASTAPI_BASE_URL:${ai.fastapi.base-url:http://localhost:8000}}")
    private String fastApiBaseUrl;

    @Bean
    public WebClient fastApiWebClient() {
        log.info("[WebClient] FastAPI baseUrl: {}", fastApiBaseUrl);
        // 커넥션 풀 설정
        ConnectionProvider connectionProvider = ConnectionProvider.builder("fastapi-pool")
            .maxConnections(100)              // 최대 커넥션 수
            .maxIdleTime(Duration.ofSeconds(20))      // 유휴 커넥션 유지 시간
            .maxLifeTime(Duration.ofSeconds(60))      // 커넥션 최대 생명 시간
            .pendingAcquireTimeout(Duration.ofSeconds(5)) // 커넥션 획득 대기 시간
            .evictInBackground(Duration.ofSeconds(120))   // 백그라운드 정리 주기
            .build();

        // HTTP 클라이언트 설정 (타임아웃, 커넥션 풀)
        // FastAPI 응답 대기 시간 연장: 연결 타임아웃 10초, 응답 타임아웃 120초
        HttpClient httpClient = HttpClient.create(connectionProvider)
            .option(ChannelOption.CONNECT_TIMEOUT_MILLIS, 10000)  // 연결 타임아웃: 10초 (5초 -> 10초로 증가)
            .responseTimeout(Duration.ofSeconds(120))              // 응답 타임아웃: 120초 (60초 -> 120초로 증가)
            .doOnConnected(conn -> conn
                .addHandlerLast(new ReadTimeoutHandler(120, TimeUnit.SECONDS))   // 읽기 타임아웃: 120초 (60초 -> 120초로 증가)
                .addHandlerLast(new WriteTimeoutHandler(10, TimeUnit.SECONDS))  // 쓰기 타임아웃: 10초
            );

        return WebClient.builder()
            .baseUrl(fastApiBaseUrl)
            .clientConnector(new ReactorClientHttpConnector(httpClient))
            .filter(logRequest())   // 요청 로깅
            .filter(logResponse())  // 응답 로깅
            .build();
    }

    // 요청 로깅 필터
    private ExchangeFilterFunction logRequest() {
        return ExchangeFilterFunction.ofRequestProcessor(clientRequest -> {
            log.info("[WebClient] 요청: {} {}", clientRequest.method(), clientRequest.url());
            return Mono.just(clientRequest);
        });
    }

    // 응답 로깅 필터
    private ExchangeFilterFunction logResponse() {
        return ExchangeFilterFunction.ofResponseProcessor(clientResponse -> {
            log.info("[WebClient] 응답: {} {}", clientResponse.statusCode(), clientResponse.headers().asHttpHeaders());
            return Mono.just(clientResponse);
        });
    }
}

