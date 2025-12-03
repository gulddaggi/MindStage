package com.ssafy.s13p21b204.notification.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.LocalDateTime;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.UpdateTimestamp;

@Entity
@Table(name = "fcm_tokens")
@Getter
@Builder
@AllArgsConstructor
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class FcmToken {

	@Id
	private Long userId; // 1:1 보장

	@Column(nullable = false, length = 2048)
	private String token;

	@Column(nullable = true, length = 100)
	private String deviceUuid; // 선택: 매핑된 워치 UUID

	@UpdateTimestamp
	private LocalDateTime updatedAt;

	public void update(String token, String deviceUuid) {
		this.token = token;
		this.deviceUuid = deviceUuid;
	}
}


