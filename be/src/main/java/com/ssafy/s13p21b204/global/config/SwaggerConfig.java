package com.ssafy.s13p21b204.global.config;

import io.swagger.v3.oas.models.Components;
import io.swagger.v3.oas.models.OpenAPI;
import io.swagger.v3.oas.models.info.Contact;
import io.swagger.v3.oas.models.info.Info;
import io.swagger.v3.oas.models.info.License;
import io.swagger.v3.oas.models.security.SecurityRequirement;
import io.swagger.v3.oas.models.security.SecurityScheme;
import io.swagger.v3.oas.models.servers.Server;
import java.util.List;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class SwaggerConfig {

  @Value("${server.port:8080}")
  private String serverPort;

  @Bean
  public OpenAPI openAPI() {
    // JWT 보안 스키마 이름
    String jwtSecuritySchemeName = "JWT";

    return new OpenAPI()
        // API 기본 정보 설정
        .info(apiInfo())
        // 서버 정보 설정
        .servers(List.of(
            new Server()
                .url("http://localhost:" + serverPort)
                .description("로컬 서버"),
            new Server()
                .url("https://mindstage.duckdns.org/")
                .description("운영 서버")
        ))
        // JWT 인증을 위한 보안 설정
        .components(new Components()
            .addSecuritySchemes(jwtSecuritySchemeName, jwtSecurityScheme()))
        // 모든 API에 JWT 인증 적용
        .addSecurityItem(new SecurityRequirement().addList(jwtSecuritySchemeName));
  }

  /**
   * API 기본 정보 설정
   */
  private Info apiInfo() {
    return new Info()
        .title("MindStage API")
        .description("MindStage 프로젝트의 REST API 문서입니다.")
        .version("1.0.0")
        .contact(new Contact()
            .email("99mini22@gmail.com"))
        .license(new License()
            .name("Apache License Version 2.0")
            .url("https://www.apache.org/licenses/LICENSE-2.0"));
  }

  /**
   * JWT 보안 스키마 설정
   * Bearer Token 방식으로 JWT 인증 구현
   */
  private SecurityScheme jwtSecurityScheme() {
    return new SecurityScheme()
        .name("Authorization")
        .type(SecurityScheme.Type.HTTP)
        .scheme("bearer")
        .bearerFormat("JWT")
        .in(SecurityScheme.In.HEADER)
        .description("JWT 토큰을 입력해주세요. (Bearer 접두사 없이 토큰만 입력)");
  }
}

