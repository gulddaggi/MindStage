package com.ssafy.s13p21b204.global.redis;

/**
 * 캐시 이름을 상수로 정의하는 클래스
 * 
 * Spring Cache의 @Cacheable 등에서 사용되는 캐시 영역을 식별합니다.
 * 상수를 사용하여 오타나 잘못된 캐시 이름 사용을 방지합니다.
 */
public class CacheNames {
  public static final String USERBYUSERNAME = "CACHE_USERBYUSERNAME";
  public static final String ALLUSERS = "CACHE_ALLUSERS";
  public static final String LOGINUSER = "CACHE_LOGINUSER";
}

