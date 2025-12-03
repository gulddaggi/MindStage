package com.ssafy.s13p21b204.watch.repository;

import com.ssafy.s13p21b204.watch.entity.GalaxyWatch;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface GalaxyWatchRepository extends JpaRepository<GalaxyWatch, Long> {
  Optional<GalaxyWatch> findByUserId(Long userId);

  boolean existsByUserId(Long userId);

  Optional<GalaxyWatch> findByUuid(String uuid);

  boolean existsByUuid(String uuid);
}
