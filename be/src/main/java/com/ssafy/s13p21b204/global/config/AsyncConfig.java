package com.ssafy.s13p21b204.global.config;

import lombok.extern.slf4j.Slf4j;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.scheduling.annotation.EnableAsync;
import org.springframework.scheduling.concurrent.ThreadPoolTaskExecutor;

import java.util.concurrent.Executor;
import java.util.concurrent.ThreadPoolExecutor;

/**
 * 비동기 처리 설정 Interview 워크플로우 백그라운드 처리용
 */
@Configuration
@EnableAsync
@Slf4j
public class AsyncConfig {

  @Bean(name = "interviewWorkflowExecutor")
  public Executor interviewWorkflowExecutor() {
    ThreadPoolTaskExecutor executor = new ThreadPoolTaskExecutor();

    executor.setCorePoolSize(5);
    executor.setMaxPoolSize(10);
    executor.setQueueCapacity(100);
    executor.setThreadNamePrefix("interview-workflow-");

    // 큐가 꽉 찬 경우 정책
    executor.setRejectedExecutionHandler(new ThreadPoolExecutor.CallerRunsPolicy());

    // 스레드 풀 종료 시 대기 중인 작업 완료
    executor.setWaitForTasksToCompleteOnShutdown(true);
    executor.setAwaitTerminationSeconds(60);

    executor.initialize();

    log.info("[AsyncConfig] Interview Workflow Executor 초기화 완료 - core: {}, max: {}, queue: {}",
        executor.getCorePoolSize(), executor.getMaxPoolSize(), executor.getQueueCapacity());

    return executor;
  }
}

