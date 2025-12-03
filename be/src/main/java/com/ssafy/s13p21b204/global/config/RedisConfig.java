package com.ssafy.s13p21b204.global.config;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.redis.connection.RedisConnectionFactory;
import org.springframework.data.redis.connection.RedisStandaloneConfiguration;
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory;
import org.springframework.data.redis.core.RedisTemplate;
import org.springframework.data.redis.serializer.GenericJackson2JsonRedisSerializer;
import org.springframework.data.redis.serializer.StringRedisSerializer;

@Configuration
public class RedisConfig {

  @Value("${spring.data.redis.host}")
  private String host;

  @Value("${spring.data.redis.port}")
  private int port;

  @Value("${spring.data.redis.password}")
  private String password;

  /**
   * RedisConnectionFactory는 Redis 서버와의 연결을 관리
   * LettuceConnectionFactory는 Lettuce 클라이언트를 사용하는 Redis 연결을 설정
   */

  /**
   * RedisTemplate: Redis data access code를 간소화하는 클래스
   * 객체들의 역직렬화/직렬화
   * binary 데이터를 Redis에 젖아
   * JdkSerializationRedisSerializer
   *
   * StringRedisSerializer: binary 데이터로 저장되기 때문에 이를 String으로 변환 UTF-8 인코딩 방식 사용
   * GenericJackson2JsonRedisSerializer는 Redis에 저장되는 객체를 JSON 형식으로 직렬화, 역직렬화
   */
   @Bean
  public RedisConnectionFactory redisConnectionFactory() {

    RedisStandaloneConfiguration config = new RedisStandaloneConfiguration();
    config.setHostName(host);
    config.setPort(port);

    if (password != null && !password.isBlank()) {
      config.setPassword(password);
    }

    return new LettuceConnectionFactory(config);
  }

  @Bean
  public RedisTemplate<String, String> redisTemplate() {
    RedisTemplate<String, String> redisTemplate = new RedisTemplate<>();
    redisTemplate.setKeySerializer(new StringRedisSerializer());
    redisTemplate.setValueSerializer(new StringRedisSerializer());
    redisTemplate.setConnectionFactory(redisConnectionFactory());

    return redisTemplate;
  }

  @Bean
  public GenericJackson2JsonRedisSerializer genericJackson2JsonRedisSerializer() {
    return new GenericJackson2JsonRedisSerializer();
  }

}
