package com.ssafy.s13p21b204.notification.repository;

import com.ssafy.s13p21b204.notification.entity.FcmToken;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface FcmTokenRepository extends JpaRepository<FcmToken, Long> {
	Optional<FcmToken> findByUserId(Long userId);
	void deleteByUserId(Long userId);
}


