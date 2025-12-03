# Interview API Documentation

This documentation provides examples of request/response patterns for all endpoints in various situations.

---

## Table of Contents
- Interview Start Endpoint
- Interview Answer Endpoint
- Interview End Endpoint
- Error Cases
- Test Mode Examples

---

---

## Interview Start Endpoint (with JD)

**Endpoint:** `POST /api/v1/interview/start`

This endpoint is used when starting an interview **with a job description (JD)**. It performs OCR on the JD document and uses it for context-aware question generation.

---

### Example 1: Initial Interview Start (No Previous Recording)

This example shows starting a new interview without any previous question/answer history.

**Request**
```json
{
    "jd_presigned_url": "https://s3.amazonaws.com/bucket/jd.pdf?X-Amz-Signature=...",
    "resume": [
        {
            "question": "Technical Skills",
            "answer": "Python, TensorFlow, AWS, Docker"
        },
        {
            "question": "Experience",
            "answer": "Led a team of 5 developers in building ML pipelines"
        }
    ],
    "qna_history": [],
    "latest_wav_file_url": null,
    "saved_tts_file_url": [
        "https://s3.amazonaws.com/bucket/tts1.wav?X-Amz-Signature=...",
        "https://s3.amazonaws.com/bucket/tts2.wav?X-Amz-Signature=..."
    ]
}
```

**Response**
```json
{
    "status": "success",
    "converted_text_with_stt": null,
    "text_from_tts": [
        "Tell me about your experience implementing ML pipelines in Python.",
        "How do you approach team leadership challenges?"
    ],
    "talker": [0, 1]
}
```

**Note:** talker `0` indicates strict interviewer, `1` indicates friendly interviewer

---

### Example 2: First Answer Submission (with STT)

This example shows submitting the first answer via audio recording and getting the next question.

**Request**
```json
{
    "jd_presigned_url": "https://s3.amazonaws.com/bucket/jd.pdf?X-Amz-Signature=...",
    "resume": [
        {
            "question": "Technical Skills",
            "answer": "Python, TensorFlow, AWS, Docker"
        },
        {
            "question": "Experience",
            "answer": "Led a team of 5 developers in building ML pipelines"
        }
    ],
    "qna_history": [
        {
            "question": "Tell me about your experience implementing ML pipelines in Python.",
            "answer": ""
        }
    ],
    "latest_wav_file_url": "https://s3.amazonaws.com/bucket/answer1.wav?X-Amz-Signature=...",
    "saved_tts_file_url": ["https://s3.amazonaws.com/bucket/tts3.wav?X-Amz-Signature=..."]
}
```

**Response**
```json
{
    "status": "success",
    "converted_text_with_stt": "I developed a scalable ML pipeline using Python and TensorFlow that processed over 1M records daily...",
    "text_from_tts": ["Can you describe how you ensured the reliability of your ML pipeline in production?"],
    "talker": [0]
}
```

**Note:** The STT transcript is automatically merged into the last unanswered question in qna_history before generating the next question.

---

## Interview Answer Endpoint (without JD)

**Endpoint:** `POST /api/v1/interview/answer`

This endpoint is used for **continuing the interview without JD context**. It's typically used for follow-up questions after the initial start.

---

### Example 1: Mid-Interview Response (Confident Technical Answer)

**Request**
```json
{
    "jd_presigned_url": null,
    "resume": [
        {
            "question": "Technical Skills",
            "answer": "Python, TensorFlow, AWS, Docker"
        }
    ],
    "qna_history": [
        {
            "question": "Tell me about your ML pipeline experience...",
            "answer": "Technical response about ML pipeline..."
        },
        {
            "question": "How did you ensure reliability?",
            "answer": ""
        }
    ],
    "latest_wav_file_url": "https://s3.amazonaws.com/bucket/answer2.wav?X-Amz-Signature=...",
    "saved_tts_file_url": ["https://s3.amazonaws.com/bucket/tts4.wav?X-Amz-Signature=..."]
}
```

**Response**
```json
{
    "status": "success",
    "converted_text_with_stt": "We implemented extensive monitoring using CloudWatch and set up automated failover...",
    "text_from_tts": ["What were some specific challenges you faced during implementation?"],
    "talker": [1]
}
```

---

### Example 2: Mid-Interview Response (Text Answer Instead of Audio)

You can also provide text answers directly without audio by filling in the `answer` field in `qna_history` and setting `latest_wav_file_url` to null.

**Request**
```json
{
    "jd_presigned_url": null,
    "resume": [
        {
            "question": "Technical Skills",
            "answer": "Python, TensorFlow, AWS, Docker"
        }
    ],
    "qna_history": [
        {
            "question": "Tell me about your ML pipeline experience...",
            "answer": "Technical response about ML pipeline..."
        },
        {
            "question": "How did you ensure reliability?",
            "answer": "We implemented extensive monitoring using CloudWatch and set up automated failover..."
        }
    ],
    "latest_wav_file_url": null,
    "saved_tts_file_url": ["https://s3.amazonaws.com/bucket/tts5.wav?X-Amz-Signature=..."]
}
```

**Response**
```json
{
    "status": "success",
    "converted_text_with_stt": null,
    "text_from_tts": ["Could you share what you learned from those deployment challenges?"],
    "talker": [1]
}
```

---

## Interview End Endpoint

**Endpoint:** `POST /api/v1/interview/end`

This endpoint generates a comprehensive interview report based on all Q&A exchanges and optional audio analysis.

---

### Example: Final Interview Assessment

**Request**
```json
{
    "jd_presigned_url": "https://s3.amazonaws.com/bucket/jd.pdf?X-Amz-Signature=...",
    "resume": [
        {
            "question": "Technical Skills",
            "answer": "Python, TensorFlow, AWS, Docker"
        },
        {
            "question": "Experience",
            "answer": "Led a team of 5 developers in building ML pipelines"
        }
    ],
    "qna_history": [
        {
            "question": "Tell me about your ML pipeline experience...",
            "answer": "Technical response..."
        },
        {
            "question": "How did you ensure reliability?",
            "answer": "Response about monitoring..."
        },
        {
            "question": "What were the challenges?",
            "answer": "Response about challenges..."
        }
    ],
    "preflight_urls": [
        "https://s3.amazonaws.com/bucket/answer1.wav?X-Amz-Signature=...",
        "https://s3.amazonaws.com/bucket/answer2.wav?X-Amz-Signature=...",
        "https://s3.amazonaws.com/bucket/answer3.wav?X-Amz-Signature=..."
    ]
}
```

**Response**
```json
{
    "status": "success",
    "result": {
        "generated_at": "2025-11-06T14:30:00",
        "overall_evaluation": "The candidate demonstrated strong technical knowledge in ML pipeline development...",
        "details": [
            {
                "qa": {
                    "question": "Tell me about your ML pipeline experience...",
                    "answer": "Technical response..."
                },
                "audio_analysis": {
                    "confidence_level": 0.85,
                    "speaking_pace": "moderate",
                    "tone": "professional"
                },
                "assessment": "The candidate showed excellent understanding of ML pipeline architecture..."
            }
        ]
    }
}
```

---

---

## Error Cases

### STT Model Not Available
```json
{
    "status": "error",
    "detail": "STT model not available"
}
```

### Failed to Generate Questions
```json
{
    "status": "error",
    "detail": "Failed to generate questions"
}
```

### Failed to Upload TTS
```json
{
    "status": "error",
    "detail": "Failed to upload TTS for question 1: Connection error"
}
```

---

---

## Test Mode Examples

When `STT_TEST_MODE=true` is set in the environment:

### STT Stub Mode
```json
{
    "status": "success",
    "converted_text_with_stt": "[STT stub] transcribed from answer1.wav",
    "text_from_tts": ["Test question 1"],
    "talker": [1]
}
```

### TTS Test Mode
When `TTS_TEST_MODE=true` is set:
- Silent WAV files are generated instead of actual TTS audio
- Useful for testing without external TTS API calls
- Files are named with timestamp and saved to temp directory

### Debug Mode
When `TTS_DEBUG=true` is set:
- Latest TTS outputs are saved to `/tmp/tts_latest/`
- Access debug player at: `GET /debug/tts`
- Lists all generated TTS files with playback links

---

## Response Field Descriptions

### InterviewResponse
- `status`: Success or error status of the request
- `converted_text_with_stt`: Transcribed text from the audio file (null if no audio provided)
- `text_from_tts`: List of generated questions for text-to-speech
- `talker`: List of interviewer types (0 for strict, 1 for friendly) for each question

### EndInterviewResponse
- `status`: Success or error status of the request
- `result`: Object containing:
  - `report_content`: Detailed interview assessment
  - `summary`: Brief summary of the interview
  - `recommendations`: Next steps recommendation

---

## Notes

**1. Pre-signed URLs:**
   - All file operations use pre-signed URLs (S3 or similar)
   - `jd_presigned_url`: For downloading JD documents (PDF/images)
   - `latest_wav_file_url`: For downloading user's audio answer
   - `saved_tts_file_url`: For uploading generated TTS audio files
   - `preflight_urls`: For downloading all interview recordings in the end endpoint

**2. Interviewer Types:**
   - `0`: Strict/formal interviewer for technical questions
   - `1`: Friendly/encouraging interviewer for behavioral questions

**3. The interviewer type is determined by:**
   - Question content (technical vs behavioral)
   - Candidate's previous responses
   - Signs of confidence or nervousness
   - Overall interview context

**4. STT (Speech-to-Text) Integration:**
   - When `latest_wav_file_url` is provided, the system downloads the audio and transcribes it using GMS Whisper API
   - The transcript is automatically merged into the last unanswered question in `qna_history`
   - If `STT_TEST_MODE=true`, a stub transcript is generated instead of calling the real API
   - The transcript is returned in `converted_text_with_stt` field

**5. JD OCR:**
   - The `/start` endpoint performs OCR on the JD document (PDF or image)
   - Supports both PDF (via pdf2image + pytesseract) and image formats
   - System dependencies required: tesseract-ocr, poppler-utils
   - OCR text is used for context-aware question generation

**6. Question Generation:**
   - Uses GMS chat API (OpenAI-compatible) for generating interview questions
   - Default model: `gpt-4o-mini`
   - Can fallback to local LLM if `USE_LOCAL_LLM=true`
   - Questions are tailored based on JD, resume, and conversation history