#test_updated.py
"""TODO : if needed then change gtts to gms call


"""

from fastapi import FastAPI, HTTPException
from fastapi.responses import FileResponse, JSONResponse, HTMLResponse
from pydantic import BaseModel
from typing import List, Dict, Any, Optional, Union
import tempfile
import os
import requests
import uuid
from datetime import datetime
import json
from pathlib import Path
import logging
import asyncio
from functools import partial
import time
import pytesseract
from PIL import Image
import io
import requests
import shutil

# Load environment variables
import os
from dotenv import load_dotenv
import logging

# Load .env file
load_dotenv()

# configure basic logging early
logging.basicConfig(
    level=os.getenv("LOG_LEVEL", "INFO"),
    format="%(asctime)s %(levelname)s [%(name)s] %(message)s"
)
logger = logging.getLogger(__name__)

# Control URL error behavior via env:
# - If true: invalid presigned URLs raise exceptions (strict)
# - If false: log warnings and continue (lenient)
STRICT_PRESIGNED_URL_ERRORS = os.getenv("STRICT_PRESIGNED_URL_ERRORS", "false").lower() == "true"

# Set up GMS API base URL for OpenAI-compatible endpoints
# GMS_KEY (same as OPENAI_API_KEY) is required for authentication
GMS_BASE_URL = os.getenv("GMS_BASE_URL", "https://gms.ssafy.io/gmsapi/api.openai.com/v1")
os.environ["OPENAI_API_BASE"] = GMS_BASE_URL

# Normalize credentials: if GMS_KEY provided, ensure OPENAI_API_KEY is set
if os.getenv("GMS_KEY") and not os.getenv("OPENAI_API_KEY"):
    os.environ["OPENAI_API_KEY"] = os.environ["GMS_KEY"]

# Ensure GMS_KEY / OPENAI_API_KEY is set
if not os.getenv("OPENAI_API_KEY"):
    logger.warning("OPENAI_API_KEY (GMS_KEY) not set. API calls may fail.")

# If requested, redirect to a local LLM server instead of GMS (for local testing)
if os.getenv("USE_LOCAL_LLM", "false").lower() == "true":
    local_base = os.getenv("LOCAL_LLM_API_BASE")
    if local_base:
        os.environ["OPENAI_API_BASE"] = local_base
        logger.info(f"Using local LLM at {local_base}")
    # Ensure an API key exists (some clients require a non-empty key)
    if not os.getenv("OPENAI_API_KEY"):
        os.environ["OPENAI_API_KEY"] = "local"

# Import question generation model
import sys
sys.path.append(str(Path(__file__).parent))
from models.question_model import generate_questions_from_context, determine_interviewer_types
from models.analysis_service import compute_intent_scores_and_emotion_labels, generate_final_report

# Request/Response Models
class OCRInput(BaseModel):
    pre_signed_url: str

class TextResponse(BaseModel):
    status: str
    text: str

class ResumeQAItem(BaseModel):
    num: Optional[str] = None
    # New/preferred shape: explicit question/answer fields
    question: str
    answer: str
    # Backwards-compatible: single 'content' field (treated as an answer)
    content: Optional[str] = None

class InterviewInput(BaseModel):
    jd_presigned_url: Optional[str] = None  # Pre-signed URL for JD PDF/image (only used in s1)
    resume: List[ResumeQAItem]
    qna_history: List[Dict[str, str]] = []  # list of {question: str, answer: str}
    latest_wav_file_url: Optional[str] = None  # Pre-signed URL for downloading user's WAV
    saved_tts_file_url: List[str] = []  # Pre-signed URLs for uploading TTS results (one-per-question)

class EndInterviewInput(BaseModel):
    jd_presigned_url: str  # Pre-signed URL for JD PDF/image
    resume: List[ResumeQAItem]
    qna_history: List[Dict[str, str]]
    preflight_urls: List[str]  # List of pre-signed URLs for downloading WAV files

class InterviewResponse(BaseModel):
    status: str
    converted_text_with_stt: Optional[str] = None
    # Back-compat for tests expecting this key
    converted_text: Optional[str] = None
    text_from_tts: List[str]
    talker: List[int]

class EndInterviewResponse(BaseModel):
    status: str
    scores: Dict[str, int]
    labels: List[int]
    report: str

from pathlib import Path

# TTS test mode: when true, generate a small silent WAV instead of calling external service
TTS_TEST_MODE = os.getenv("TTS_TEST_MODE", "true").lower() == "true"
# When enabled, keep only the latest request's generated TTS files in a debug folder
TTS_DEBUG_KEEP_LATEST = os.getenv("TTS_DEBUG_KEEP_LATEST", "false").lower() == "true"
TTS_DEBUG_DIR = Path(os.getenv("TTS_DEBUG_DIR", "/tmp/tts_latest"))

# configure basic logging (moved earlier in file, kept here for reference)
# logging.basicConfig(
#     level=os.getenv("LOG_LEVEL", "INFO"),
#     format="%(asctime)s %(levelname)s [%(name)s] %(message)s"
# )
# logger = logging.getLogger(__name__)

def _clear_tts_debug_dir():
    """Clear the TTS debug directory contents."""
    try:
        if TTS_DEBUG_DIR.exists():
            logger.info(f"TTS debug: clearing existing directory {TTS_DEBUG_DIR}")
            for p in TTS_DEBUG_DIR.glob("*"):
                try:
                    p.unlink()
                    logger.info(f"TTS debug: removed old file {p.name}")
                except Exception as e:
                    logger.warning(f"Failed to remove debug TTS file {p}: {e}")
        else:
            logger.info(f"TTS debug: creating directory {TTS_DEBUG_DIR}")
            TTS_DEBUG_DIR.mkdir(parents=True, exist_ok=True)
            logger.info(f"TTS debug: directory created successfully")
    except Exception as e:
        logger.error(f"Failed to prepare TTS debug dir {TTS_DEBUG_DIR}: {e}", exc_info=True)

def _normalize_resume_list(resume_list: List[ResumeQAItem]) -> List[Dict[str, str]]:
    """Normalize incoming resume items into explicit question/answer dicts.

    Supports three shapes:
      - {'question':..., 'answer':...} (preferred)
      - {'num':..., 'content':...} (back-compat)
      - partial items: will be coerced to labelled entries
    Emits a warning when the old 'content' shape is used.
    """
    normalized = []
    for qa in resume_list:
        # Prefer explicit question/answer if provided
        if getattr(qa, 'question', None) and getattr(qa, 'answer', None):
            normalized.append({"question": qa.question, "answer": qa.answer})
            continue

        # Backwards-compatible: if 'content' exists, treat it as an answer
        if getattr(qa, 'content', None):
            q_label = f"Resume item {qa.num}" if getattr(qa, 'num', None) else "Resume detail"
            logger.warning("Deprecated resume shape used: 'content' field detected for %s; treat as answer.", q_label)
            normalized.append({"question": q_label, "answer": qa.content})
            continue

        # Last-resort: use num as question label with empty answer
        q_label = f"Resume item {qa.num}" if getattr(qa, 'num', None) else "Resume item"
        normalized.append({"question": q_label, "answer": ""})

    return normalized

async def _extract_jd_text_from_presigned_url(presigned_url: str) -> str:
    """
    Download JD from pre-signed URL and extract text using Tesseract OCR.
    Supports PDF and image formats.
    Returns extracted text.
    """
    local_path = None
    try:
        # Download file from pre-signed URL
        t0 = time.perf_counter()
        response = requests.get(presigned_url, timeout=(5, 30))
        response.raise_for_status()
        logger.info(f"JD OCR: downloaded file in {time.perf_counter() - t0:.3f}s ({len(response.content)} bytes)")
        
        # Detect file type from content or URL
        content_type = response.headers.get('Content-Type', '').lower()
        is_pdf = 'pdf' in content_type or presigned_url.lower().endswith('.pdf')
        
        if is_pdf:
            # Handle PDF: convert to images and OCR each page
            try:
                from pdf2image import convert_from_bytes
                local_path = f"/tmp/jd_{uuid.uuid4()}.pdf"
                with open(local_path, 'wb') as f:
                    f.write(response.content)
                
                start_ocr = time.perf_counter()
                # Convert PDF to images
                images = convert_from_bytes(response.content, dpi=200)
                logger.info(f"JD OCR: converted PDF to {len(images)} page(s)")
                
                # OCR each page
                text_parts = []
                for idx, img in enumerate(images):
                    page_text = pytesseract.image_to_string(img, lang='kor+eng')
                    text_parts.append(f"[Page {idx+1}]\n{page_text}")
                    logger.info(f"JD OCR: processed page {idx+1}/{len(images)}")
                
                full_text = "\n\n".join(text_parts)
                logger.info(f"JD OCR: tesseract processed PDF in {time.perf_counter() - start_ocr:.3f}s")
                return full_text
            except ImportError:
                logger.warning("pdf2image not installed, falling back to image-only OCR")
                raise HTTPException(
                    status_code=500,
                    detail="PDF processing requires pdf2image library. Install with: pip install pdf2image"
                )
        else:
            # Handle image file
            local_path = f"/tmp/jd_{uuid.uuid4()}.png"
            with open(local_path, 'wb') as f:
                f.write(response.content)
            
            start_ocr = time.perf_counter()
            image = Image.open(local_path)
            text = pytesseract.image_to_string(image, lang='kor+eng')
            logger.info(f"JD OCR: tesseract processed image in {time.perf_counter() - start_ocr:.3f}s")
            return text
            
    except Exception as e:
        logger.exception(f"JD OCR extraction failed: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to extract JD text: {str(e)}")
    finally:
        # Cleanup downloaded file
        if local_path and os.path.exists(local_path):
            try:
                os.remove(local_path)
            except Exception as e:
                logger.warning(f"Failed to cleanup JD file {local_path}: {e}")

async def text_to_speech(text: str, lang: str = 'en', talker: int = 0) -> str:
    """
    Convert text to speech. In test mode, generate a silent WAV stub.
    
    Args:
        text: Text to convert to speech
        lang: Language code (not used for GMS TTS)
        talker: Interviewer type (0: strict/male, 1: friendly/female)
    
    Returns: path to temporary wav file
    """
    try:
        # Create unique filename
        temp_file = f"/tmp/tts_{uuid.uuid4()}.mp3"
        if TTS_TEST_MODE:
            # generate 0.5s of silence at 16000 Hz, 16-bit mono
            import wave
            import struct
            framerate = 16000
            duration_s = 0.5
            nframes = int(framerate * duration_s)
            with wave.open(temp_file, 'w') as wf:
                wf.setnchannels(1)
                wf.setsampwidth(2)
                wf.setframerate(framerate)
                for _ in range(nframes):
                    wf.writeframes(struct.pack('<h', 0))
            return temp_file
        # Use GMS TTS API
        GMS_KEY = os.getenv("GMS_KEY") or os.getenv("OPENAI_API_KEY")
        TTS_API_URL = "https://gms.ssafy.io/gmsapi/api.openai.com/v1/audio/speech"
        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {GMS_KEY}"
        }
        # Select voice based on talker type
        # 0: strict interviewer -> echo (male voice)
        # 1: friendly interviewer -> nova (female voice)
        voice = "echo" if talker == 0 else "nova"
        payload = {
            "model": "gpt-4o-mini-tts",
            "input": text,
            "voice": voice,
            "response_format": "mp3"
        }
        start_tts = time.perf_counter()
        response = requests.post(TTS_API_URL, headers=headers, json=payload)
        response.raise_for_status()
        with open(temp_file, "wb") as f:
            f.write(response.content)
        logger.info(f"GMS TTS generated with voice '{voice}' in {time.perf_counter() - start_tts:.3f}s -> {temp_file}")
        return temp_file
    except Exception as e:
        logger.exception(f"TTS generation failed: {e}")
        raise HTTPException(status_code=500, detail="Failed to generate speech")


# async def generate_questions(context: Dict[str, Any]) -> List[str]:
#     """
#     Generate interview questions based on context using language model
#     Returns list of questions
#     """
#     try:
#         #  Replace with actual model call
#         # questions = your_language_model.generate_questions(context)
#         dummy_questions = [
#             "Tell me about your experience with Python development.",
#             "How do you handle challenging team dynamics?",
#             "What interests you about this position?"
#         ]
#         return dummy_questions
#     except Exception as e:
#         print(f"Error generating questions: {e}")
#         raise HTTPException(status_code=500, detail="Failed to generate questions")

# async def generate_next_question(context: Dict[str, Any]) -> str:
#     """
#     Generate next question based on conversation context
#     """
#     try:
#         # : Replace with actual model call
#         # question = your_language_model.generate_next_question(context)
#         dummy_question = "What would you do differently in your last project?"
#         return dummy_question
#     except Exception as e:
#         print(f"Error generating next question: {e}")
#         raise HTTPException(status_code=500, detail="Failed to generate next question")



# Note: S3 direct helpers removed. This app uses pre-signed URLs (HTTP GET/PUT) for uploads and downloads.


app = FastAPI()
# STT test mode: when true, use a deterministic stub instead of calling GMS Whisper API
STT_TEST_MODE = os.getenv("STT_TEST_MODE", "true").lower() == "true"

def _stt_stub(wav_path: str) -> str:
    """Simple deterministic STT stub for tests."""
    # Optionally use filename to vary output
    base = Path(wav_path).name
    return f"[STT stub] transcribed from {base}"

async def _transcribe_with_gms_whisper(audio_file_path: str) -> str:
    """
    Transcribe audio using GMS Whisper API.
    
    Args:
        audio_file_path: Path to local audio file
        
    Returns:
        Transcribed text
    """
    gms_key = os.getenv("OPENAI_API_KEY")
    if not gms_key:
        raise HTTPException(status_code=500, detail="GMS_KEY (OPENAI_API_KEY) not configured")
    
    # GMS Whisper endpoint
    whisper_url = f"{GMS_BASE_URL}/audio/transcriptions"
    
    try:
        start_time = time.perf_counter()
        with open(audio_file_path, 'rb') as audio_file:
            files = {
                'file': (Path(audio_file_path).name, audio_file, 'audio/wav')
            }
            data = {
                'model': 'whisper-1'
            }
            headers = {
                'Authorization': f'Bearer {gms_key}'
            }
            
            response = requests.post(
                whisper_url,
                headers=headers,
                files=files,
                data=data,
                timeout=(5, 30)
            )
            response.raise_for_status()
            result = response.json()
            text = result.get('text', '')
            
            logger.info(f"GMS Whisper: transcribed in {time.perf_counter() - start_time:.3f}s")
            return text
            
    except Exception as e:
        logger.exception(f"GMS Whisper transcription failed: {e}")
        raise HTTPException(status_code=500, detail=f"Whisper API error: {str(e)}")

class OCRRequest(BaseModel):
    s3_key: str
    ocr_type: str = "tesseract"  # "tesseract" or "custom"

# --- Shared helpers to de-duplicate s1/s2 logic ---
def _maybe_prepare_tts_debug_dir():
    """If debug mode is on, clear the TTS debug directory."""
    if TTS_DEBUG_KEEP_LATEST:
        logger.info(
            f"TTS debug mode enabled: TTS_DEBUG_KEEP_LATEST={TTS_DEBUG_KEEP_LATEST}, TTS_DEBUG_DIR={TTS_DEBUG_DIR}"
        )
        _clear_tts_debug_dir()

async def _handle_stt_if_provided(latest_wav_file_url: Optional[str]) -> str:
    """Download and transcribe WAV if URL provided; respects STT_TEST_MODE. Returns transcribed text or ''."""
    if not latest_wav_file_url:
        return ""
    if STT_TEST_MODE:
        start_stub = time.perf_counter()
        text = _stt_stub(latest_wav_file_url)
        logger.info(f"STT stub executed in {time.perf_counter() - start_stub:.3f}s")
        return text
    local_wav = None
    try:
        t0 = time.perf_counter()
        response = requests.get(latest_wav_file_url, timeout=(5, 15))
        response.raise_for_status()
        logger.info(
            f"STT: downloaded WAV in {time.perf_counter() - t0:.3f}s ({len(response.content)} bytes)"
        )
        local_wav = f"/tmp/input_{uuid.uuid4()}.wav"
        with open(local_wav, 'wb') as f:
            f.write(response.content)
        # Transcribe with GMS Whisper API
        return await _transcribe_with_gms_whisper(local_wav)
    except Exception as e:
        msg = f"STT: failed to process presigned URL {latest_wav_file_url}: {e}"
        if STRICT_PRESIGNED_URL_ERRORS:
            logger.error(msg)
            raise
        logger.warning(msg + "; omitting STT for this request")
        return ""
    finally:
        if local_wav and os.path.exists(local_wav):
            os.remove(local_wav)

def _merge_stt_into_qna(qna_history_input: List[Dict[str, str]] | None, converted_text: str, stage_label: str) -> List[Dict[str, str]]:
    """If converted_text present and last QnA has no answer, merge it in; return a new list."""
    qna_history = [dict(item) for item in (qna_history_input or [])]
    if not converted_text:
        return qna_history
    try:
        stt_text = converted_text.strip()
        if stt_text and qna_history:
            last = qna_history[-1]
            if not last.get("answer"):
                last["answer"] = stt_text
                logger.info(f"{stage_label}: merged STT answer into last QnA item")
        elif stt_text and not qna_history:
            logger.info(f"{stage_label}: received STT but no pending question in history; skipping merge")
    except Exception as e:
        logger.warning(f"{stage_label}: failed to merge STT into QnA history: {e}")
    return qna_history

def _build_context(jd_text: str, resume_list: List[ResumeQAItem], qna_history: List[Dict[str, str]]) -> Dict[str, Any]:
    """Normalize resume and return context dict."""
    normalized_resume = _normalize_resume_list(resume_list)
    return {
        "jd": jd_text,
        "resume": normalized_resume,
        "qna": qna_history,
    }

async def _generate_and_upload_tts_for_questions(questions: List[str], upload_urls: List[str], talker_types: List[int]):
    """Generate TTS for questions and upload to provided pre-signed URLs in order.
    
    Args:
        questions: List of questions to generate TTS for
        upload_urls: List of pre-signed URLs to upload to
        talker_types: List of talker types (0: strict/male, 1: friendly/female) for each question
    """
    provided_urls = upload_urls or []
    for idx, question in enumerate(questions):
        if idx >= len(provided_urls):
            logger.info(f"No upload URL provided for question {idx+1}; skipping TTS upload")
            continue
        upload_url = provided_urls[idx]
        # Get talker type for this question (default to 0 if not provided)
        talker = talker_types[idx] if idx < len(talker_types) else 0
        logger.info(f"Uploading TTS for Q{idx+1} (talker={talker}) to: {upload_url}")
        tts_file = None
        try:
            # Generate Korean TTS to match Korean questions
            tts_file = await text_to_speech(question, lang='ko', talker=talker)
            logger.info(
                f"TTS: generated file at {tts_file}, exists={os.path.exists(tts_file) if tts_file else False}"
            )
            # If debug mode, copy generated file to the debug folder
            if TTS_DEBUG_KEEP_LATEST and tts_file and os.path.exists(tts_file):
                try:
                    dest = TTS_DEBUG_DIR / f"q{idx+1}.wav"
                    logger.info(f"TTS debug: attempting to copy {tts_file} -> {dest}")
                    shutil.copy(tts_file, dest)
                    logger.info(
                        f"TTS debug: saved latest TTS to {dest}, size={dest.stat().st_size} bytes"
                    )
                except Exception as e:
                    logger.error(f"TTS debug: failed to save copy: {e}", exc_info=True)
            with open(tts_file, 'rb') as f:
                headers = {"Content-Type": "audio/wav"}
                up0 = time.perf_counter()
                resp = requests.put(upload_url, data=f, headers=headers, timeout=(5, 30))
                resp.raise_for_status()
            logger.info(f"Uploaded TTS for question {idx+1} in {time.perf_counter() - up0:.3f}s")
        except Exception as e:
            # Either raise or warn depending on strictness
            if STRICT_PRESIGNED_URL_ERRORS:
                logger.error(f"TTS upload failed for Q{idx+1}: {e}")
                raise
            logger.warning(f"TTS upload skipped for Q{idx+1} due to error: {e}", exc_info=True)
        finally:
            # Only remove temp file if not in debug mode
            if not TTS_DEBUG_KEEP_LATEST and tts_file and os.path.exists(tts_file):
                os.remove(tts_file)

def _generate_talker_types(n: int) -> List[int]:
    import random
    return [random.randint(0, 1) for _ in range(n)]

# --- Async helper for potentially blocking question generation ---
async def generate_questions_async(context: Dict[str, Any], n_questions: int):
    """Run synchronous generate_questions_from_context in a thread to avoid blocking event loop.

    The underlying function can perform network I/O (LLM API) or heavy local inference.
    Offloading prevents event loop starvation under concurrency.
    """
    loop = asyncio.get_running_loop()
    return await loop.run_in_executor(None, partial(generate_questions_from_context, context, n_questions=n_questions))

@app.post("/api/v1/interview/start", response_model=InterviewResponse)
async def s1(input_data: InterviewInput):
    """
    Generate initial interview questions based on JD and resume.
    JD is provided as a pre-signed URL and will be OCR'd.
    Process speech-to-text if wav file provided.
    Upload TTS result to provided pre-signed URL.
    """
    try:
        converted_text = ""

        # Prepare TTS debug folder: keep only the latest request's files
        _maybe_prepare_tts_debug_dir()
        
        # Extract JD text from pre-signed URL (OCR)
        jd_text = ""
        if input_data.jd_presigned_url:
            logger.info("S1: Extracting JD text from pre-signed URL")
            try:
                jd_text = await _extract_jd_text_from_presigned_url(input_data.jd_presigned_url)
                logger.info(f"S1: Extracted JD text ({len(jd_text)} chars)")
            except Exception as e:
                if STRICT_PRESIGNED_URL_ERRORS:
                    logger.error(f"S1: JD OCR failed: {e}")
                    raise
                jd_text = ""
                logger.warning(f"S1: JD OCR failed (url may be invalid); proceeding without JD. Error: {e}")
        else:
            logger.warning("S1: No JD pre-signed URL provided, proceeding without JD context")
        
        # Handle STT if wav file provided (non-initial stage)
        converted_text = await _handle_stt_if_provided(input_data.latest_wav_file_url)
        if converted_text:
            preview = converted_text[:160].replace('\n', ' ')
            logger.info(f"S1: STT converted_text len={len(converted_text)} preview={preview!r}{' ...' if len(converted_text)>160 else ''}")
        else:
            logger.info("S1: No STT converted_text received (empty or not provided)")
        
        # Merge STT result into the last QnA item if it has no answer yet
        qna_history = _merge_stt_into_qna(input_data.qna_history, converted_text, "S1")

        # Format context for question generation
        context = _build_context(jd_text, input_data.resume, qna_history)
        
        # Determine number of questions based on provided URLs
        provided_urls = input_data.saved_tts_file_url or []
        n_questions = len(provided_urls) if provided_urls else 1
        
        # Generate next question(s)
        start_llm = time.perf_counter()
        questions = await generate_questions_async(context, n_questions=n_questions)
        logger.info(f"LLM: generated {len(questions)} question(s) in {time.perf_counter() - start_llm:.3f}s")
        for q in questions:
            logger.info(f"Generated question: {q}")
        if not questions:
            raise HTTPException(status_code=500, detail="Failed to generate questions")
        
        # Determine interviewer type for ALL questions in a single batch call
        try:
            talker_types = determine_interviewer_types(questions, jd_text, qna_history)
            # Ensure length matches
            if len(talker_types) != len(questions):
                logger.warning("S1: interviewer_types length mismatch; falling back to random for remaining")
                import random
                while len(talker_types) < len(questions):
                    talker_types.append(random.randint(0, 1))
            logger.info("S1: Determined interviewer types per question: %s", talker_types)
        except Exception as e:
            # If the batch determination fails, fall back to random per question
            import random
            talker_types = [random.randint(0, 1) for _ in questions]
            logger.warning(f"S1: interviewer_type batch determination unavailable; using random types: {talker_types}. Error: {e}")
        
        # Generate and upload TTS files. If multiple questions are generated,
        # we'll upload up to the number of provided URLs (in order).
        await _generate_and_upload_tts_for_questions(questions, provided_urls, talker_types)
        
        # Store generated questions
        generated_questions = questions
            
        return InterviewResponse(
            status="success",
            converted_text_with_stt=converted_text if input_data.latest_wav_file_url else None,
            converted_text=converted_text if input_data.latest_wav_file_url else None,
            text_from_tts=generated_questions,
            talker=talker_types
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/api/v1/interview/answer", response_model=InterviewResponse)
async def s2(input_data: InterviewInput):
    """
    Generate next interview question based on resume and QnA history only.
    Does NOT use JD to avoid overhead (JD was already processed in s1).
    Process speech-to-text if wav file provided.
    Upload TTS result to provided pre-signed URL.
    """
    try:
        converted_text = ""

        # Prepare TTS debug folder: keep only the latest request's files
        _maybe_prepare_tts_debug_dir()
        
        # Handle STT if wav file provided
        converted_text = await _handle_stt_if_provided(input_data.latest_wav_file_url)
        if converted_text:
            preview = converted_text[:160].replace('\n', ' ')
            logger.info(f"S2: STT converted_text len={len(converted_text)} preview={preview!r}{' ...' if len(converted_text)>160 else ''}")
        else:
            logger.info("S2: No STT converted_text received (empty or not provided)")
        
        # Merge STT result into the last QnA item if it has no answer yet
        qna_history = _merge_stt_into_qna(input_data.qna_history, converted_text, "S2")

        # Format context WITHOUT JD (to avoid overhead)
        context = _build_context("", input_data.resume, qna_history)
        
        # Determine number of questions based on provided URLs
        provided_urls = input_data.saved_tts_file_url or []
        n_questions = len(provided_urls) if provided_urls else 1
        
        # Generate next question(s)
        start_llm = time.perf_counter()
        questions = await generate_questions_async(context, n_questions=n_questions)
        logger.info(f"LLM: generated {len(questions)} question(s) in {time.perf_counter() - start_llm:.3f}s")
        for q in questions:
            logger.info(f"Generated question: {q}")
        if not questions:
            raise HTTPException(status_code=500, detail="Failed to generate questions")
        
        # Generate talker types (0: strict, 1: friendly) BEFORE TTS generation
        talker_types = _generate_talker_types(len(questions))
        
        # Generate and upload TTS files
        await _generate_and_upload_tts_for_questions(questions, provided_urls, talker_types)
        
        # Store generated questions
        generated_questions = questions
            
        return InterviewResponse(
            status="success",
            converted_text_with_stt=converted_text if input_data.latest_wav_file_url else None,
            converted_text=converted_text if input_data.latest_wav_file_url else None,
            text_from_tts=generated_questions,
            talker=talker_types
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/api/v1/interview/end", response_model=EndInterviewResponse)
async def s3(input_data: EndInterviewInput):
    """
    Generate final interview report including audio analysis.
    JD is provided as a pre-signed URL and will be OCR'd.
    Returns comprehensive interview assessment.
    """
    try:
        # Extract JD text from pre-signed URL (OCR)
        logger.info("S3: Extracting JD text from pre-signed URL")
        try:
            jd_text = await _extract_jd_text_from_presigned_url(input_data.jd_presigned_url)
            logger.info(f"S3: Extracted JD text ({len(jd_text)} chars)")
        except Exception as e:
            if STRICT_PRESIGNED_URL_ERRORS:
                logger.error(f"S3: JD OCR failed: {e}")
                raise
            jd_text = ""
            logger.warning(f"S3: JD OCR failed (url may be invalid); proceeding without JD. Error: {e}")
        
        # Compute intent scores and emotion labels from all Q&A answers
        logger.info("S3: Computing intent scores and emotion labels")
        scores, labels = await compute_intent_scores_and_emotion_labels(input_data.qna_history)
        logger.info(f"S3: Intent scores computed: {scores}")
        logger.info(f"S3: Emotion labels count: {len(labels)}")
        
        # Generate final report text combining scores, labels, and LLM analysis
        logger.info("S3: Generating final report")
        report_text = generate_final_report(
            scores=scores,
            emotion_labels=labels,
            qna_history=input_data.qna_history,
            jd_text=jd_text
        )
        logger.info(f"S3: Final report generated ({len(report_text)} chars)")
        
        # Return structured response
        return EndInterviewResponse(
            status="success",
            scores=scores,
            labels=labels,
            report=report_text
        )
                    
    except Exception as e:
        logger.exception(f"Error generating report: {str(e)}")
        raise HTTPException(
            status_code=500, 
            detail=f"Failed to generate interview report: {str(e)}"
        )

# Basic request timing middleware
@app.middleware("http")
async def log_request_time(request, call_next):
    start = time.perf_counter()
    response = await call_next(request)
    duration = time.perf_counter() - start
    logger.info(f"HTTP {request.method} {request.url.path} -> {response.status_code} in {duration:.3f}s")
    return response

# --- STT-only endpoint ---
class STTOnlyRequest(BaseModel):
    stt_url: str

class STTOnlyResponse(BaseModel):
    status: str
    converted_text: str

@app.post("/api/v1/stt", response_model=STTOnlyResponse)
async def transcribe_audio(req: STTOnlyRequest):
    """
    Standalone STT endpoint: download audio from presigned URL and return transcribed text.
    
    Request:
      {
        "stt_url": "https://..."
      }
    
    Response:
      {
        "status": "success",
        "converted_text": "transcribed text"
      }
    """
    try:
        logger.info(f"STT-only request: stt_url={req.stt_url}")
        converted_text = await _handle_stt_if_provided(req.stt_url)
        return STTOnlyResponse(status="success", converted_text=converted_text)
    except Exception as e:
        if STRICT_PRESIGNED_URL_ERRORS:
            logger.error(f"STT-only: failed to process URL {req.stt_url}: {e}")
            raise HTTPException(status_code=500, detail=f"STT failed: {str(e)}")
        logger.warning(f"STT-only: failed to process URL {req.stt_url}: {e}; returning empty transcription")
        return STTOnlyResponse(status="success", converted_text="")

# --- Debug endpoints to inspect latest generated TTS files ---
@app.get("/debug/tts")
async def list_latest_tts():
    if not TTS_DEBUG_KEEP_LATEST:
        return JSONResponse(status_code=404, content={"detail": "TTS debug is disabled"})
    try:
        files = sorted([p.name for p in TTS_DEBUG_DIR.glob("*.wav")]) if TTS_DEBUG_DIR.exists() else []
        
        # Return HTML player page for browser viewing
        html = """
        <!DOCTYPE html>
        <html>
        <head>
            <title>TTS Debug Player</title>
            <style>
                body { font-family: Arial, sans-serif; padding: 20px; background: #f5f5f5; }
                .container { max-width: 800px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
                h1 { color: #333; border-bottom: 2px solid #4CAF50; padding-bottom: 10px; }
                .file-item { margin: 15px 0; padding: 15px; background: #f9f9f9; border-radius: 4px; border-left: 4px solid #4CAF50; }
                .file-name { font-weight: bold; color: #333; margin-bottom: 8px; }
                audio { width: 100%; margin-top: 8px; }
                .info { color: #666; font-size: 0.9em; margin-top: 10px; padding: 10px; background: #e3f2fd; border-radius: 4px; }
                .download-btn { display: inline-block; margin-top: 8px; padding: 6px 12px; background: #2196F3; color: white; text-decoration: none; border-radius: 4px; font-size: 0.9em; }
                .download-btn:hover { background: #1976D2; }
            </style>
        </head>
        <body>
            <div class="container">
                <h1>🎵 Latest Generated TTS Files</h1>
                <div class="info">
                    📁 Directory: """ + str(TTS_DEBUG_DIR) + """<br>
                    📊 Files found: """ + str(len(files)) + """
                </div>
        """
        
        if files:
            for fname in files:
                html += f"""
                <div class="file-item">
                    <div class="file-name">🔊 {fname}</div>
                    <audio controls preload="metadata">
                        <source src="/debug/tts/{fname}" type="audio/wav">
                        Your browser does not support the audio element.
                    </audio>
                    <a href="/debug/tts/{fname}" class="download-btn" download>⬇️ Download</a>
                </div>
                """
        else:
            html += """
                <div class="file-item">
                    <div class="file-name">No TTS files found</div>
                    <p>Make a request to /api/v1/interview/start with TTS_DEBUG_KEEP_LATEST=true to generate files.</p>
                </div>
            """
        
        html += """
            </div>
        </body>
        </html>
        """
        
        return HTMLResponse(content=html)
        
    except Exception as e:
        logger.exception(f"Failed to list debug TTS files: {e}")
        raise HTTPException(status_code=500, detail="Failed to list debug TTS files")

@app.get("/debug/tts/{filename}")
async def get_latest_tts_file(filename: str):
    if not TTS_DEBUG_KEEP_LATEST:
        return JSONResponse(status_code=404, content={"detail": "TTS debug is disabled"})
    path = TTS_DEBUG_DIR / filename
    if not path.exists() or not path.is_file():
        raise HTTPException(status_code=404, detail="File not found")
    return FileResponse(str(path), media_type="audio/wav")

