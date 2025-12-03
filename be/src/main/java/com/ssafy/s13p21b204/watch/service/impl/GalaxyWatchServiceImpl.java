package com.ssafy.s13p21b204.watch.service.impl;

import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.watch.dto.GalaxyRequestDto;
import com.ssafy.s13p21b204.watch.dto.GalaxyResponseDto;
import com.ssafy.s13p21b204.watch.dto.GalaxyUpdateDto;
import com.ssafy.s13p21b204.watch.entity.GalaxyWatch;
import com.ssafy.s13p21b204.watch.repository.GalaxyWatchRepository;
import com.ssafy.s13p21b204.watch.service.GalaxyWatchService;
import com.ssafy.s13p21b204.notification.service.FirebasePushService;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@Slf4j
@RequiredArgsConstructor
public class GalaxyWatchServiceImpl implements GalaxyWatchService {

  private final GalaxyWatchRepository galaxyWatchRepository;
  private final FirebasePushService firebasePushService;

  @Override
  @Transactional
  public GalaxyResponseDto registerWatch(Long userId, GalaxyRequestDto galaxyRequestDto) {
    log.info("[GalaxyWatchService] 워치 등록 시도");
    if (galaxyWatchRepository.existsByUserId(userId)) {
      log.warn("[GalaxyWatchService] 워치 등록 실패 - 기존 디바이스가 등록되어있습니다.");
      throw ApiException.of(HttpStatus.CONFLICT, ErrorMessage.DEVICE_ALREADY_REGISTERED);
    }
    GalaxyWatch galaxyWatch = GalaxyWatch.builder()
        .userId(userId)
        .uuid(galaxyRequestDto.uuid())
        .modelName(galaxyRequestDto.modelName())
        .build();
    galaxyWatchRepository.save(galaxyWatch);
    log.info("[GalaxyWatchService] 워치 등록 완료");
    return GalaxyResponseDto.from(galaxyWatch);
  }

  @Override
  @Transactional
  public GalaxyResponseDto updateWatch(Long userId, GalaxyUpdateDto updateDto) {
    log.info("[GalaxyWatchService] 워치 정보 업데이트 시도");

    GalaxyWatch watch = galaxyWatchRepository.findByUserId(userId)
        .orElseThrow(() -> {
          log.warn("[GalaxyWatchService] 워치 업데이트 실패 - 등록된 워치 없음");
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.DEVICE_NOT_FOUND);
        });

    watch.changeModel(updateDto.modelName(), updateDto.uuid());
    log.info("[GalaxyWatchService] 워치 정보 업데이트 완료");
    return GalaxyResponseDto.from(watch);
  }

  @Override
  @Transactional(readOnly = true)
  public GalaxyResponseDto getWatch(Long userId) {
    log.info("[GalaxyWatchService] 갤럭시 워치 조회 시도");
    GalaxyWatch existGalaxyWatch = galaxyWatchRepository.findByUserId(userId).orElse(null);
    if (existGalaxyWatch == null) {
      log.info("[GalaxyWatchService] 갤럭시 워치 조회 완료 - 등록된 워치 없음");
      return null;
    }
    log.info("[GalaxyWatchService] 갤럭시 워치 조회 완료");
    return GalaxyResponseDto.from(existGalaxyWatch);
  }

  @Override
  @Transactional
  public void deleteWatch(Long userId) {
    log.info("[GalaxyWatchService] 워치 정보 삭제 시도");
    GalaxyWatch watch = galaxyWatchRepository.findByUserId(userId)
        .orElseThrow(() -> {
          log.warn("[GalaxyWatchService] 워치 정보 삭제 실패 - 워치 없음");
          return ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.DEVICE_NOT_FOUND);
        });
    galaxyWatchRepository.delete(watch);

    // 연동된 FCM 토큰 제거(DB+Redis)
    try {
      firebasePushService.deleteToken(userId);
    } catch (Exception e) {
      log.warn("[GalaxyWatchService] 워치 삭제 중 FCM 토큰 제거 경고 - userId={}", userId, e);
    }

    log.info("[GalaxyWatchService] 워치 정보 삭제 완료");
  }


}
