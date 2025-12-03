import os
import threading
import time
import http.server
import socketserver
from pathlib import Path
import requests
import pytest
from fastapi.testclient import TestClient
from PIL import Image, ImageDraw, ImageFont

# Ensure tests import the app
import sys
sys.path.append(str(Path(__file__).resolve().parents[1]))
from test_updated import app

MOCK_DIR = Path("/tmp/mock_presigned")
PORT = 9000

def create_mock_jd_image():
    """Create a simple test JD image with text for OCR"""
    jd_path = MOCK_DIR / "jd" / "test_jd.png"
    jd_path.parent.mkdir(parents=True, exist_ok=True)
    
    # Create a simple image with text
    img = Image.new('RGB', (800, 600), color='white')
    draw = ImageDraw.Draw(img)
    
    # Add text (use default font)
    text = """Job Description
    
Position: Python Developer
Requirements:
- 3+ years Python experience
- FastAPI knowledge
- AWS experience preferred
"""
    draw.text((50, 50), text, fill='black')
    img.save(jd_path)
    return jd_path

class MockHandler(http.server.SimpleHTTPRequestHandler):
    def do_PUT(self):
        dest = MOCK_DIR / self.path.lstrip('/')
        dest.parent.mkdir(parents=True, exist_ok=True)
        length = int(self.headers.get('Content-Length', 0))
        data = self.rfile.read(length) if length else self.rfile.read()
        with open(dest, 'wb') as f:
            f.write(data)
        self.send_response(200)
        self.end_headers()

    def do_GET(self):
        file_path = MOCK_DIR / self.path.lstrip('/')
        if file_path.exists():
            self.send_response(200)
            self.send_header('Content-Type', 'application/octet-stream')
            self.end_headers()
            with open(file_path, 'rb') as f:
                self.wfile.write(f.read())
        else:
            self.send_response(404)
            self.end_headers()


def run_mock_server():
    MOCK_DIR.mkdir(parents=True, exist_ok=True)
    with socketserver.TCPServer(("127.0.0.1", PORT), MockHandler) as httpd:
        httpd.serve_forever()


@pytest.fixture(scope="module")
def client():
    # Start mock server
    server_thread = threading.Thread(target=run_mock_server, daemon=True)
    server_thread.start()
    time.sleep(0.5)  # wait for server to start
    
    # Create mock JD image
    create_mock_jd_image()
    
    with TestClient(app) as c:
        yield c


def test_interview_start_initial_uploads(client):
    # Ensure mock directory empty for uploads
    upload_dir = MOCK_DIR / "upload"
    if upload_dir.exists():
        for p in upload_dir.rglob('*'):
            if p.is_file():
                p.unlink()
    
    # Call endpoint with JD pre-signed URL and three upload URLs
    payload = {
        "jd_presigned_url": f"http://127.0.0.1:{PORT}/jd/test_jd.png",
        "resume": [{"num": "1", "question": "Experience summary", "answer": "Test resume"}],
        "qna_history": [],
        "saved_tts_file_url": [
            f"http://127.0.0.1:{PORT}/upload/questions/q1.wav",
            f"http://127.0.0.1:{PORT}/upload/questions/q2.wav",
            f"http://127.0.0.1:{PORT}/upload/questions/q3.wav"
        ]
    }
    r = client.post("/api/v1/interview/start", json=payload)
    assert r.status_code == 200
    data = r.json()
    assert data["status"] == "success"
    # Check that uploads happened
    for i in range(1,4):
        path = MOCK_DIR / f"upload/questions/q{i}.wav"
        assert path.exists(), f"Expected uploaded file at {path}"


def test_interview_answer_stt_and_tts_upload(client):
    # Prepare a WAV file to be served by mock server
    src_path = MOCK_DIR / "files/questions/response1.wav"
    src_path.parent.mkdir(parents=True, exist_ok=True)
    src_path.write_bytes(b"RIFF....dummywav")

    # s2 does NOT use JD (to avoid overhead), so jd_presigned_url can be omitted
    payload = {
        "resume": [{"num": "1", "question": "Experience summary", "answer": "Test resume"}],
        "qna_history": [{"question": "Q1", "answer": "A1"}],
        "latest_wav_file_url": f"http://127.0.0.1:{PORT}/files/questions/response1.wav",
        "saved_tts_file_url": [f"http://127.0.0.1:{PORT}/upload/questions/next_q.wav"]
    }
    r = client.post("/api/v1/interview/answer", json=payload)
    assert r.status_code == 200
    data = r.json()
    assert data["status"] == "success"
    # STT stub should produce a predictable string
    assert "[STT stub]" in data["converted_text"]
    # Check TTS uploaded
    uploaded = MOCK_DIR / "upload/questions/next_q.wav"
    assert uploaded.exists()


def test_interview_end_with_jd_ocr(client):
    # Prepare WAV files for analysis
    wav_path = MOCK_DIR / "analysis/response1.wav"
    wav_path.parent.mkdir(parents=True, exist_ok=True)
    wav_path.write_bytes(b"RIFF....dummywav")
    
    payload = {
        "jd_presigned_url": f"http://127.0.0.1:{PORT}/jd/test_jd.png",
        "resume": [{"num": "1", "question": "Experience summary", "answer": "Test resume"}],
        "qna_history": [
            {"question": "Tell me about your experience", "answer": "I worked on Python projects"}
        ],
        "preflight_urls": [f"http://127.0.0.1:{PORT}/analysis/response1.wav"]
    }
    r = client.post("/api/v1/interview/end", json=payload)
    assert r.status_code == 200
    data = r.json()
    assert data["status"] == "success"
    # Updated: response structure is flat, not nested under "result"
    assert "scores" in data
    assert "labels" in data
    assert "report" in data
    # Validate scores
    assert isinstance(data["scores"], dict)
    assert "Job_Competency" in data["scores"]
    # Validate labels (should be List[int])
    assert isinstance(data["labels"], list)
    for label in data["labels"]:
        assert isinstance(label, int)
