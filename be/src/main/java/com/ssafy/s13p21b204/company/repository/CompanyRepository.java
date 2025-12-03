package com.ssafy.s13p21b204.company.repository;

import com.ssafy.s13p21b204.company.entity.Company;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface CompanyRepository extends JpaRepository<Company,Long> {
  boolean existsByName(String companyName);

}
