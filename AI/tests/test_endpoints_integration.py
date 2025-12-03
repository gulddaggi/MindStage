import os
import threading
import time
import requests
import json
from pathlib import Path
import pytest
from fastapi.testclient import TestClient
from PIL import Image, ImageDraw

# Ensure tests import the app
import sys
sys.path.append(str(Path(__file__).resolve().parents[1]))
from test_updated import app

# Load environment variables
from dotenv import load_dotenv
load_dotenv()

# Get endpoint from environment or use default
PRESIGNED_URL_ENDPOINT = os.getenv('SPRING_ENDPOINT', "https://mindstage.duckdns.org/api/s3/presigned-url")

def create_test_jd_image():
    """Create a test JD image with text for OCR"""
    tmp_path = Path("/tmp/test_jd.png")
    img = Image.new('RGB', (800, 600), color='white')
    draw = ImageDraw.Draw(img)
    text = """Job Description
    
Position: Senior Python Developer
Requirements:
- 5+ years Python experience
- FastAPI and async programming
- AWS cloud services
- Team leadership
"""
    draw.text((50, 50), text, fill='black')
    img.save(tmp_path)
    return tmp_path

def get_presigned_url(filename, directory):
    """Get a pre-signed URL from the Spring endpoint"""
    payload = {
        "fileName": filename,
        "fileType": "upload",
        "directory": directory
    }
    response = requests.post(PRESIGNED_URL_ENDPOINT, json=payload)
    if response.status_code != 200:
        pytest.skip(f"Failed to get presigned URL: {response.text}")
    data = response.json()
    if not data.get("success"):
        pytest.skip(f"Spring endpoint error: {data.get('message')}")
    return data["data"]["presignedUrl"]

def upload_jd_image_and_get_url():
    """Upload JD test image and return its pre-signed URL"""
    # Create test JD image
    jd_image_path = create_test_jd_image()
    
    # Get pre-signed URL for upload
    upload_url = get_presigned_url("test_jd.png", "jd")
    
    # Upload the image
    with open(jd_image_path, 'rb') as f:
        response = requests.put(upload_url, data=f.read())
        if response.status_code != 200:
            pytest.skip(f"Failed to upload JD image: {response.status_code}")
    
    # Get download URL (reuse the upload URL or get a new one)
    # Note: For S3, the upload URL can often be used for download too
    return upload_url.split('?')[0]  # Remove query params to get base URL

@pytest.fixture(scope="module")
def presigned_urls():
    """Fixture to get all needed presigned URLs before running tests"""
    urls = {}
    
    # Get JD URL with uploaded image
    try:
        urls['jd'] = upload_jd_image_and_get_url()
    except Exception as e:
        pytest.skip(f"Failed to setup JD image: {e}")
    
    # Get URLs for TTS files
    tts_urls = []
    for i in range(3):
        url = get_presigned_url(f"tts_{i}.wav", "questions")
        tts_urls.append(url)
    urls['tts'] = tts_urls
    
    return urls

@pytest.fixture(scope="module")
def client():
    with TestClient(app) as c:
        yield c

def test_interview_start_with_real_urls(client, presigned_urls):
    """Test /interview/start with real pre-signed URLs including JD OCR"""
    payload = {
        "jd_presigned_url": presigned_urls['jd'],
        "resume": [{"num": "1", "question": "Experience summary", "answer": "Test resume for S3 integration"}],
        "qna_history": [],
        "saved_tts_file_url": presigned_urls['tts'][:3]
    }
    
    r = client.post("/api/v1/interview/start", json=payload)
    assert r.status_code == 200
    data = r.json()
    assert data["status"] == "success"

@pytest.fixture(autouse=True)
def setup_test_mode():
    """Setup test mode for all tests"""
    # Store original values
    original_stt = os.getenv("STT_TEST_MODE")
    original_tts = os.getenv("TTS_TEST_MODE")
    
    # Set test mode
    os.environ["STT_TEST_MODE"] = "true"
    os.environ["TTS_TEST_MODE"] = "true"
    
    yield
    
    # Restore original values if they existed
    if original_stt is not None:
        os.environ["STT_TEST_MODE"] = original_stt
    else:
        os.environ.pop("STT_TEST_MODE", None)
        
    if original_tts is not None:
        os.environ["TTS_TEST_MODE"] = original_tts
    else:
        os.environ.pop("TTS_TEST_MODE", None)

def test_interview_answer_with_real_urls(client, presigned_urls):
    """Test /interview/answer with real pre-signed URLs (no JD to avoid overhead)"""
    # Create a more complete test WAV file
    # WAV header for a silent 1-second 44.1kHz mono file
    test_wav_data = (
        b'RIFF'    # ChunkID
        b'\x24\x00\x00\x00'  # ChunkSize (36 + SubChunk2Size)
        b'WAVE'    # Format
        b'fmt '    # Subchunk1ID
        b'\x10\x00\x00\x00'  # Subchunk1Size (16 for PCM)
        b'\x01\x00'  # AudioFormat (1 for PCM)
        b'\x01\x00'  # NumChannels (1 for mono)
        b'\x44\xAC\x00\x00'  # SampleRate (44100)
        b'\x88\x58\x01\x00'  # ByteRate
        b'\x02\x00'  # BlockAlign
        b'\x10\x00'  # BitsPerSample (16)
        b'data'    # Subchunk2ID
        b'\x00\x00\x00\x00'  # Subchunk2Size (0 for empty)
    )
    
    # Get a new URL for the response audio
    response_url = get_presigned_url("response.wav", "questions")
    
    # Upload test audio to the first URL
    upload_response = requests.put(presigned_urls['tts'][0], data=test_wav_data)
    assert upload_response.status_code == 200, "Failed to upload test audio"

    try:
        # s2 does NOT include jd_presigned_url (to avoid OCR overhead)
        payload = {
            "resume": [{"num": "1", "question": "Skills summary", "answer": "Python, FastAPI, AWS"}],
            "qna_history": [{"question": "Tell me about your experience", "answer": "I've worked on several Python projects"}],
            "latest_wav_file_url": presigned_urls['tts'][0],
            "saved_tts_file_url": [response_url]
        }    
        print("Sending payload:", json.dumps(payload, indent=2))
        r = client.post("/api/v1/interview/answer", json=payload)
        print("Response status:", r.status_code)
        print("Response body:", r.text)
        assert r.status_code == 200
        data = r.json()
        assert data["status"] == "success"
        assert "[STT stub]" in data["converted_text"]  # Verify STT stub is working
    except Exception as e:
        print(f"Test failed with error: {str(e)}")
        raise

def test_presigned_url_functionality():
    """Test the pre-signed URL functionality directly"""
    # Get a test URL
    test_url = get_presigned_url("test.txt", "test")
    
    # Try uploading a small file
    test_data = b"Hello, S3!"
    response = requests.put(test_url, data=test_data)
    assert response.status_code == 200, "Failed to upload to pre-signed URL"

def test_interview_end_with_jd_ocr(client, presigned_urls):
    """Test /interview/end with JD OCR and report generation"""
    # Create test WAV for analysis
    test_wav_data = b'RIFF\x24\x00\x00\x00WAVEfmt \x10\x00\x00\x00\x01\x00\x01\x00\x44\xAC\x00\x00\x88\x58\x01\x00\x02\x00\x10\x00data\x00\x00\x00\x00'
    wav_url = get_presigned_url("analysis.wav", "analysis")
    requests.put(wav_url, data=test_wav_data)
    
    payload = {
        "jd_presigned_url": presigned_urls['jd'],
        "resume": [{"num": "1", "question": "Experience summary", "answer": "Python developer with 5 years experience"}],
        "qna_history": [
            {"question": "Tell me about your projects", "answer": "Built FastAPI microservices"}
        ],
        "preflight_urls": [wav_url]
    }
    
    r = client.post("/api/v1/interview/end", json=payload)
    assert r.status_code == 200
    data = r.json()
    assert data["status"] == "success"
    # Updated: response structure is flat, not nested under "result"
    assert "scores" in data
    assert "labels" in data
    assert "report" in data
    # Validate response structure
    assert isinstance(data["scores"], dict)
    assert "Job_Competency" in data["scores"]
    assert isinstance(data["labels"], list)
