package com.ssafy.s13p21b204.security.filter;

import com.ssafy.s13p21b204.security.service.HmacAuthService;
import com.ssafy.s13p21b204.security.exception.HmacAuthenticationException;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletRequestWrapper;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.io.ByteArrayInputStream;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Base64;
import java.util.Set;
import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.util.StreamUtils;
import org.springframework.web.filter.OncePerRequestFilter;
import org.springframework.web.servlet.HandlerExceptionResolver;
import jakarta.servlet.ServletInputStream;
import java.nio.charset.Charset;

@Slf4j
@RequiredArgsConstructor
public class HmacVerificationFilter extends OncePerRequestFilter {

  private static final String H_HEADER_UUID = "X-Watch-UUID";
  private static final String H_HEADER_TS = "X-Timestamp";
  private static final String H_HEADER_NONCE = "X-Nonce";
  private static final String H_HEADER_SHA256 = "X-Content-SHA256";
  private static final String H_HEADER_SIG = "X-Signature";

  private static final long MAX_SKEW_MS = 5 * 60 * 1000L; // ±5분
  private static final Set<String> PROTECTED_PATHS = Set.of(
      "/api/GalaxyWatch/token/commit",
      "/api/heartbeat/batch"
  );

  private final HmacAuthService hmacAuthService;
  private final HandlerExceptionResolver handlerExceptionResolver;

  @Override
  protected boolean shouldNotFilter(HttpServletRequest request) throws ServletException {
    if ("OPTIONS".equalsIgnoreCase(request.getMethod())) {
      return true;
    }
    String path = request.getRequestURI();
    return !PROTECTED_PATHS.contains(path);
  }

  @Override
  protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response, FilterChain filterChain)
      throws ServletException, IOException {
    String uuid = request.getHeader(H_HEADER_UUID);
    String tsStr = request.getHeader(H_HEADER_TS);
    String nonce = request.getHeader(H_HEADER_NONCE);
    String contentSha = request.getHeader(H_HEADER_SHA256);
    String signature = request.getHeader(H_HEADER_SIG);

    // Bootstrap 허용: 비밀키가 아직 없는 최초 커밋은 서명 없이 통과시킬 수 있다.
    // 단, uuid는 반드시 필요.
    String existingSecret = (uuid != null && !uuid.isBlank())
        ? hmacAuthService.getDeviceSecret(uuid)
        : null;
    boolean secretExists = existingSecret != null && !existingSecret.isBlank();
    if (!secretExists) {
      filterChain.doFilter(request, response);
      return;
    }

    // 필수 헤더 확인
    if (isBlank(uuid) || isBlank(tsStr) || isBlank(nonce) || isBlank(contentSha) || isBlank(signature)) {
      reject(request, response, HttpStatus.BAD_REQUEST, "Missing HMAC headers");
      return;
    }

    long now = System.currentTimeMillis();
    long ts;
    try {
      ts = Long.parseLong(tsStr);
    } catch (NumberFormatException e) {
      reject(request, response, HttpStatus.BAD_REQUEST, "Invalid timestamp");
      return;
    }
    if (Math.abs(now - ts) > MAX_SKEW_MS) {
      reject(request, response, HttpStatus.FORBIDDEN, "Timestamp skew too large");
      return;
    }

    // 바디 해시 검증 - 원본 스트림을 한 번 읽어 바이트로 보관
    byte[] bodyBytes = StreamUtils.copyToByteArray(request.getInputStream());
    String serverSha = sha256Hex(bodyBytes);
    if (!constantTimeEquals(serverSha, contentSha)) {
      reject(request, response, HttpStatus.UNAUTHORIZED, "Content SHA mismatch");
      return;
    }

    // 논스 중복 방지
    if (!hmacAuthService.registerNonce(uuid, nonce, 300L)) {
      reject(request, response, HttpStatus.FORBIDDEN, "Replay detected");
      return;
    }

    // Canonical string 구성: METHOD \n PATH_WITH_QUERY \n TIMESTAMP \n NONCE \n CONTENT_SHA
    String pathWithQuery = request.getRequestURI() +
        (request.getQueryString() != null ? "?" + request.getQueryString() : "");
    String canonical = String.join("\n",
        request.getMethod().toUpperCase(),
        pathWithQuery,
        tsStr,
        nonce,
        contentSha
    );

    // 서명 검증
    String expected = hmacBase64(existingSecret, canonical);
    if (!constantTimeEquals(expected, signature)) {
      reject(request, response, HttpStatus.UNAUTHORIZED, "Invalid signature");
      return;
    }

    // 컨트롤러에서 다시 읽을 수 있도록 재노출하는 래퍼 생성
    HttpServletRequestWrapper reusable = new HttpServletRequestWrapper(request) {
      @Override
      public ServletInputStream getInputStream() {
        ByteArrayInputStream bais = new ByteArrayInputStream(bodyBytes != null ? bodyBytes : new byte[0]);
        return new ServletInputStream() {
          @Override
          public int read() {
            return bais.read();
          }
          @Override
          public boolean isFinished() {
            return bais.available() == 0;
          }
          @Override
          public boolean isReady() {
            return true;
          }
          @Override
          public void setReadListener(jakarta.servlet.ReadListener readListener) {
            // no-op
          }
        };
      }

      @Override
      public BufferedReader getReader() {
        Charset cs = getCharacterEncoding() != null ? Charset.forName(getCharacterEncoding()) : StandardCharsets.UTF_8;
        return new BufferedReader(new InputStreamReader(getInputStream(), cs));
      }
    };

    filterChain.doFilter(reusable, response);
  }

  private static boolean isBlank(String s) {
    return s == null || s.isBlank();
  }

  private void reject(HttpServletRequest req, HttpServletResponse res, HttpStatus status, String msg)
      throws IOException {
    handlerExceptionResolver.resolveException(
        req, res, null, new HmacAuthenticationException(status, msg));
  }

  private static String sha256Hex(byte[] body) {
    try {
      MessageDigest md = MessageDigest.getInstance("SHA-256");
      byte[] dig = md.digest(body == null ? new byte[0] : body);
      StringBuilder sb = new StringBuilder(dig.length * 2);
      for (byte b : dig) {
        sb.append(String.format("%02x", b));
      }
      return sb.toString();
    } catch (Exception e) {
      return "";
    }
  }

  private static String hmacBase64(String secret, String canonical) {
    try {
      Mac mac = Mac.getInstance("HmacSHA256");
      mac.init(new SecretKeySpec(secret.getBytes(StandardCharsets.UTF_8), "HmacSHA256"));
      byte[] sig = mac.doFinal(canonical.getBytes(StandardCharsets.UTF_8));
      return Base64.getEncoder().encodeToString(sig);
    } catch (Exception e) {
      return "";
    }
  }

  private static boolean constantTimeEquals(String a, String b) {
    if (a == null || b == null) return false;
    if (a.length() != b.length()) return false;
    int result = 0;
    for (int i = 0; i < a.length(); i++) {
      result |= a.charAt(i) ^ b.charAt(i);
    }
    return result == 0;
  }
}


