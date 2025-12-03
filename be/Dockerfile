FROM eclipse-temurin:17-jre

WORKDIR /app

COPY app.jar /app/app.jar

ENV SPRING_PROFILES_ACTIVE=prod
ENV TZ=Asia/Seoul

EXPOSE 8080
ENTRYPOINT ["java", "-jar", "/app/app.jar"]
