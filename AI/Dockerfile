FROM python:3.11-slim

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    poppler-utils \
    tesseract-ocr \
    tesseract-ocr-eng \
    tesseract-ocr-kor \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# 공통 환경
ENV TZ=Asia/Seoul
ENV PYTHONUNBUFFERED=1

CMD ["uvicorn", "test_updated:app", "--host", "0.0.0.0", "--port", "8000"]

