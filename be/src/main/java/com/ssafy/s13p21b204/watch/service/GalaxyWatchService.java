package com.ssafy.s13p21b204.watch.service;

import com.ssafy.s13p21b204.watch.dto.GalaxyRequestDto;
import com.ssafy.s13p21b204.watch.dto.GalaxyResponseDto;
import com.ssafy.s13p21b204.watch.dto.GalaxyUpdateDto;

public interface GalaxyWatchService {
  public GalaxyResponseDto registerWatch(Long userId, GalaxyRequestDto galaxyRequestDto);

  public GalaxyResponseDto updateWatch(Long userId, GalaxyUpdateDto galaxyUpdateDto);

  public GalaxyResponseDto getWatch(Long userId);

  public void deleteWatch(Long userId);
}
