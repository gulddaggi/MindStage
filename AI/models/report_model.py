"""Report generation model.

⚠️ OBSOLETE: This module is no longer used in the current codebase.
              Replaced by analysis_service.py for final report generation.

Original purpose:
- Generate detailed per-question interview reports
- Combine text and audio analysis
- Support PDF/text output

Current replacement: models/analysis_service.py
- Provides aggregate NCS competency scoring
- Generates comprehensive final reports with standardized criteria
- Async-safe for FastAPI endpoints

This file is kept for reference only and may be removed in future cleanup.
Last used: Prior to feature/ai branch
"""

from typing import Dict, Any, List
import os
from pathlib import Path
from datetime import datetime
import time
import logging
from threading import Lock

logger = logging.getLogger(__name__)
try:
    from fpdf import FPDF
    _HAS_FPDF = True
except Exception:
    FPDF = None
    _HAS_FPDF = False

# Try importing LangChain (prefer chat models for GMS)
_HAS_LANGCHAIN = True
_HAS_CHAT = True
try:
    from langchain.prompts import PromptTemplate
    from langchain.chains import LLMChain
    try:
        from langchain.chat_models import init_chat_model, ChatOpenAI
    except Exception:
        try:
            from langchain.chat_models import ChatOpenAI  # type: ignore
            init_chat_model = None  # type: ignore
        except Exception:
            _HAS_CHAT = False
    # Keep legacy LLM import for fallback if needed
    try:
        from langchain_community.llms import OpenAI  # type: ignore
    except Exception:
        try:
            from langchain.llms import OpenAI  # type: ignore
        except Exception:
            OpenAI = None  # type: ignore
except Exception:
    _HAS_LANGCHAIN = False
    _HAS_CHAT = False

_OPENAI_CHAT = None
from threading import Lock as _Lock
_OPENAI_CHAT_LOCK = _Lock()

# Optional KoBERT analysis
_KOBERT_ENABLE = os.getenv("KOBERT_ANALYSIS", "true").lower() == "true"
_INTENT_CACHE = {"loaded": False, "model": None, "tokenizer": None, "label_map": None}
_INTENT_LOCK: Lock = Lock()

def _ensure_intent_loaded():
    if _INTENT_CACHE["loaded"]:
        return
    with _INTENT_LOCK:
        if _INTENT_CACHE["loaded"]:
            return
        try:
            from . import intent_model as im
            repo_root = Path(__file__).resolve().parents[2]
            model_path = os.getenv(
                "INTENT_MODEL_PATH",
                str(repo_root / "ai" / "v1_code" / "using_custom_models" / "model_intent_quantized.pt"),
            )
            label_map_path = os.getenv(
                "INTENT_LABEL_MAP_PATH",
                str(repo_root / "ai" / "v1_code" / "using_custom_models" / "label_map.txt"),
            )
            t_load = time.perf_counter()
            tokenizer = im.get_tokenizer()  # type: ignore
            model, label_map = im.load_quantized_model(model_path, label_map_path)  # type: ignore
            load_dur = time.perf_counter() - t_load
            _INTENT_CACHE.update({"loaded": True, "model": model, "tokenizer": tokenizer, "label_map": label_map})
            try:
                labels_preview = list(label_map.values()) if isinstance(label_map, dict) else label_map
            except Exception:
                labels_preview = "<unknown labels>"
            logger.info(f"KoBERT intent model loaded for reporting in {load_dur:.3f}s; labels={labels_preview}")
        except Exception as e:
            logger.warning(f"KoBERT intent model load failed: {e}")
            _INTENT_CACHE["loaded"] = False

def _safe_analyze_emotion(text: str) -> Dict[str, Any] | None:
    if not _KOBERT_ENABLE or not text:
        try:
            logger.info(
                f"KoBERT emotion skipped: enabled={_KOBERT_ENABLE}, has_text={bool(text)}"
            )
        except Exception:
            pass
        return None
    try:
        from . import emotion_model as emo
        sents = emo.split_sentences(text) if hasattr(emo, "split_sentences") else [text]
        t_emo = time.perf_counter()
        sent_results, para = emo.predict_sentences(sents)  # type: ignore
        dur_emo = time.perf_counter() - t_emo
        if not para:
            res = sent_results[0] if sent_results else None
            if not res:
                return None
            probs = res.get("probs", {})
            out = {
                "label": "neutral" if res.get("pred_label") in ("uncertain", "neutral") else res.get("pred_label"),
                "probs": {
                    "positive": float(probs.get("positive", 0.0)),
                    "neutral": float(probs.get("uncertain", probs.get("neutral", 0.0))),
                    "negative": float(probs.get("negative", 0.0)),
                },
            }
            try:
                lp = out["probs"]
                logger.info(
                    f"KoBERT emotion took {dur_emo:.3f}s: label={out['label']} (pos={lp.get('positive',0):.2f}, neu={lp.get('neutral',0):.2f}, neg={lp.get('negative',0):.2f})"
                )
            except Exception:
                pass
            return out
        plabel = para.get("paragraph_pred_label")
        probs = para.get("paragraph_probs", {})
        out = {
            "label": "neutral" if plabel in ("uncertain", "neutral") else plabel,
            "probs": {
                "positive": float(probs.get("positive", 0.0)),
                "neutral": float(probs.get("uncertain", probs.get("neutral", 0.0))),
                "negative": float(probs.get("negative", 0.0)),
            },
        }
        try:
            lp = out["probs"]
            logger.info(
                f"KoBERT emotion took {dur_emo:.3f}s: label={out['label']} (pos={lp.get('positive',0):.2f}, neu={lp.get('neutral',0):.2f}, neg={lp.get('negative',0):.2f})"
            )
        except Exception:
            pass
        return out
    except Exception as e:
        logger.warning(f"KoBERT emotion analysis failed: {e}")
        return None

def _safe_analyze_intent(text: str) -> Dict[str, Any] | None:
    if not _KOBERT_ENABLE or not text:
        try:
            logger.info(
                f"KoBERT intent skipped: enabled={_KOBERT_ENABLE}, has_text={bool(text)}"
            )
        except Exception:
            pass
        return None
    try:
        _ensure_intent_loaded()
        if not _INTENT_CACHE["loaded"]:
            return None
        im = __import__(__name__.replace("report_model", "intent_model"), fromlist=['*'])
        model = _INTENT_CACHE["model"]
        tok = _INTENT_CACHE["tokenizer"]
        label_map = _INTENT_CACHE["label_map"]
        t_int = time.perf_counter()
        # Assume four-return signature of predict_intent
        pred_label, conf, probs, top2 = im.predict_intent(model, tok, text, label_map)  # type: ignore
        dur_int = time.perf_counter() - t_int
        probs_map = {label_map[i]: float(probs[i]) for i in range(len(probs))}
        out = {"top_label": pred_label, "confidence": float(conf), "probs": probs_map, "top2": top2}
        try:
            top3 = sorted(probs_map.items(), key=lambda x: x[1], reverse=True)[:3]
            logger.info(
                f"KoBERT intent took {dur_int:.3f}s: top={pred_label} conf={float(conf):.2f}; top3="
                + ", ".join(f"{k}={v:.2f}" for k, v in top3)
            )
        except Exception:
            pass
        return out
    except Exception as e:
        logger.warning(f"KoBERT intent analysis failed: {e}")
        return None

def _get_openai_chat():
    global _OPENAI_CHAT
    if _OPENAI_CHAT is not None:
        return _OPENAI_CHAT
    if not _HAS_LANGCHAIN or not _HAS_CHAT:
        return None
    with _OPENAI_CHAT_LOCK:
        if _OPENAI_CHAT is not None:
            return _OPENAI_CHAT
        model_name = os.getenv("OPENAI_MODEL", "gpt-4o-mini")
        base_url = os.environ.get("OPENAI_API_BASE", "https://gms.ssafy.io/gmsapi/api.openai.com/v1")
        try:
            use_init = os.getenv("USE_INIT_CHAT_MODEL", "false").lower() == "true"
            if use_init and 'init_chat_model' in globals() and callable(init_chat_model):  # type: ignore
                try:
                    _OPENAI_CHAT = init_chat_model(model_name, model_provider="openai")  # type: ignore
                    logger.info(f"Initialized chat model via init_chat_model: {model_name}")
                    return _OPENAI_CHAT
                except Exception as e:
                    logger.warning(f"init_chat_model failed, falling back to ChatOpenAI: {e}")
            # Prefer provider signature when available
            api_key = os.getenv("OPENAI_API_KEY") or os.getenv("GMS_KEY")
            try:
                _OPENAI_CHAT = ChatOpenAI(model=model_name, base_url=base_url, api_key=api_key)  # type: ignore
                logger.info(f"Initialized ChatOpenAI (provider) model: {model_name} with base {base_url}")
            except Exception:
                _OPENAI_CHAT = ChatOpenAI(model=model_name, openai_api_base=base_url)  # type: ignore
                logger.info(f"Initialized ChatOpenAI (legacy) model: {model_name} with base {base_url}")
        except Exception as e:
            logger.warning(f"Failed to initialize chat model: {e}")
            _OPENAI_CHAT = None
        return _OPENAI_CHAT

def _mock_audio_analysis(wav_path: str) -> Dict[str, Any]:
    """
    Mock function to simulate audio analysis.
    To be replaced with actual audio model analysis.
    """
    return {
        "confidence_level": 0.85,
        "speaking_pace": "moderate",
        "tone": "professional",
        "hesitations": 2,
        "filler_words": ["um", "like"],
        "emotion_scores": {
            "confident": 0.8,
            "nervous": 0.2,
            "enthusiastic": 0.7
        }
    }

def _format_audio_insights(audio_analysis: Dict[str, Any]) -> str:
    """Format audio analysis into readable text."""
    return (
        f"Speech Analysis:\n"
        f"- Confidence Level: {audio_analysis['confidence_level']*100:.1f}%\n"
        f"- Speaking Pace: {audio_analysis['speaking_pace']}\n"
        f"- Tone: {audio_analysis['tone']}\n"
        f"- Number of Hesitations: {audio_analysis['hesitations']}\n"
        f"- Emotional Indicators:\n"
        f"  * Confidence: {audio_analysis['emotion_scores']['confident']*100:.1f}%\n"
        f"  * Enthusiasm: {audio_analysis['emotion_scores']['enthusiastic']*100:.1f}%\n"
    )

def generate_interview_report(
    context: Dict[str, Any],
    audio_files: List[str],
    output_path: str | None
) -> str | Dict[str, Any]:
    """
    Generate comprehensive interview report combining text and audio analysis.
    
    Parameters:
    - context: Dict with 'jd', 'resume', and 'qna' (same as question_model)
    - audio_files: List of paths to WAV files containing interview responses
    - output_path: Where to save the PDF report
    
    Returns: Path to generated PDF report
    """
    # Timings collection
    timings_enabled = os.getenv("INCLUDE_TIMINGS", "true").lower() == "true"
    t_start = time.perf_counter()
    timings: Dict[str, Any] = {"per_item": []}

    # Extract context
    jd_text = context.get("jd", "")
    resume = context.get("resume", [])
    qna_history = context.get("qna", [])

    # Analyze each QnA pair with corresponding audio
    detailed_analysis = []
    for i, (qa, audio_file) in enumerate(zip(qna_history, audio_files)):
        # Get mock audio analysis (replace with real model later)
        t_audio = time.perf_counter()
        audio_analysis = _mock_audio_analysis(audio_file)
        audio_dur = time.perf_counter() - t_audio
        logger.info(f"Audio analysis (mock) for item {i+1} took {audio_dur:.3f}s")
        
        # Prepare analysis prompt (include KoBERT text analysis)
        analysis_prompt = (
            "You are an expert interview assessor. Analyze this interview response:\n"
            "Question: {question}\n"
            "Answer: {answer}\n\n"
            "Audio Analysis:\n{audio_insights}\n\n"
            "KoBERT Text Analysis:\n{kobert_text}\n\n"
            "Job Description Context:\n{jd}\n\n"
            "Provide a detailed assessment of:\n"
            "1. Answer relevance and completeness\n"
            "2. Communication clarity\n"
            "3. Technical accuracy\n"
            "4. Areas for improvement\n"
            "Keep the analysis professional and constructive."
            "use korean."
        )

        # Get response analysis (either from local HF model, LangChain or mock)
        use_local = os.getenv("USE_LOCAL_LLM", "false").lower() == "true"
        kobert_text = None
        t_kobert = time.perf_counter()
        try:
            kobert_text = _format_kobert_text(qa["answer"]) if qa.get("answer") else "(no text analysis)"
        except Exception:
            kobert_text = "(no text analysis)"
        kobert_dur = time.perf_counter() - t_kobert
        if kobert_text:
            logger.info(f"KoBERT text analysis for item {i+1} took {kobert_dur:.3f}s")

        if use_local:
            try:
                from transformers import pipeline
                import torch

                model_name = os.getenv("LOCAL_LLM_MODEL", "gpt2")
                gen = pipeline(
                    "text-generation",
                    model=model_name,
                    device="cpu",
                    torch_dtype=torch.float32,  # Avoid dynamic features
                    config={'use_cache': True}
                )
                prompt_text = analysis_prompt.format(
                    question=qa["question"],
                    answer=qa["answer"],
                    audio_insights=_format_audio_insights(audio_analysis),
                    kobert_text=kobert_text,
                    jd=jd_text
                )
                t_llm = time.perf_counter()
                out = gen(prompt_text, max_length=512, do_sample=True, temperature=0.7, num_return_sequences=1)
                analysis = out[0]["generated_text"]
                llm_dur = time.perf_counter() - t_llm
                logger.info(f"Local transformers analysis took {llm_dur:.3f}s")
            except Exception as e:
                logger.warning(f"Local LLM analysis failed or transformers not available: {e}")
                analysis = "Analysis currently unavailable"
        elif _HAS_LANGCHAIN and os.getenv("OPENAI_API_KEY"):
            try:
                template = PromptTemplate(
                    input_variables=["question", "answer", "audio_insights", "jd"],
                    template=analysis_prompt
                )
                llm = _get_openai_chat()
                if llm is None:
                    raise RuntimeError("Chat model unavailable")
                chain = LLMChain(llm=llm, prompt=template)
                t_openai = time.perf_counter()
                analysis = chain.run({
                    "question": qa["question"],
                    "answer": qa["answer"],
                    "audio_insights": _format_audio_insights(audio_analysis),
                    "kobert_text": kobert_text,
                    "jd": jd_text
                })
                llm_dur = time.perf_counter() - t_openai
                logger.info(f"OpenAI analysis took {llm_dur:.3f}s")
            except Exception as e:
                logger.warning(f"LLM analysis failed: {e}")
                analysis = "Analysis currently unavailable"
        else:
            # Mock analysis for testing
            analysis = (
                f"Analysis of Response {i+1}:\n"
                f"1. Answer Relevance: The response directly addresses the question\n"
                f"2. Communication: Clear and structured delivery\n"
                f"3. Technical Accuracy: Demonstrates good understanding\n"
                f"4. Improvement Areas: Could provide more specific examples\n"
            )
            llm_dur = 0.0

        if timings_enabled:
            timings["per_item"].append({
                "index": i + 1,
                "audio_s": round(audio_dur, 4),
                "kobert_s": round(kobert_dur, 4),
                "llm_s": round(llm_dur if 'llm_dur' in locals() else 0.0, 4),
            })

        detailed_analysis.append({
            "qa": qa,
            "audio_analysis": audio_analysis,
            "assessment": analysis
        })

    # Generate overall evaluation prompt
    overall_prompt = (
        "You are an expert interview assessor. Based on the following interview data:\n"
        "Job Description:\n{jd}\n\n"
        "Interview Performance:\n{detailed_results}\n\n"
        "Provide a comprehensive evaluation including:\n"
        "1. Overall interview performance\n"
        "2. Key strengths demonstrated\n"
        "3. Areas for improvement\n"
        "4. Fit for the role\n"
        "5. Specific recommendations\n"
        "Be professional, balanced, and constructive in your assessment."
    )

    # Get overall evaluation
    use_local = os.getenv("USE_LOCAL_LLM", "false").lower() == "true"
    if use_local:
        try:
            from transformers import pipeline
            import torch

            model_name = os.getenv("LOCAL_LLM_MODEL", "gpt2")
            device = 0 if torch.cuda.is_available() else -1
            gen = pipeline("text-generation", model=model_name, device=device)
            prompt_text = overall_prompt.format(
                jd=jd_text,
                detailed_results="\n\n".join(
                    f"Q{i+1}: {a['assessment']}" for i, a in enumerate(detailed_analysis)
                )
            )
            t_llm2 = time.perf_counter()
            out = gen(prompt_text, max_length=512, do_sample=True, temperature=0.7, num_return_sequences=1)
            overall_evaluation = out[0]["generated_text"]
            overall_llm_dur = time.perf_counter() - t_llm2
            logger.info(f"Local transformers overall evaluation took {overall_llm_dur:.3f}s")
        except Exception as e:
            logger.warning(f"Local LLM overall evaluation failed or transformers not available: {e}")
            overall_evaluation = "Overall evaluation currently unavailable"
    elif _HAS_LANGCHAIN and os.getenv("OPENAI_API_KEY"):
        try:
            template = PromptTemplate(
                input_variables=["jd", "detailed_results"],
                template=overall_prompt
            )
            llm = _get_openai_chat()
            if llm is None:
                raise RuntimeError("Chat model unavailable")
            chain = LLMChain(llm=llm, prompt=template)
            t_openai2 = time.perf_counter()
            overall_evaluation = chain.run({
                "jd": jd_text,
                "detailed_results": "\n\n".join(
                    f"Q{i+1}: {a['assessment']}" 
                    for i, a in enumerate(detailed_analysis)
                )
            })
            overall_llm_dur = time.perf_counter() - t_openai2
            logger.info(f"OpenAI overall evaluation took {overall_llm_dur:.3f}s")
        except Exception as e:
            logger.warning(f"LLM evaluation failed: {e}")
            overall_evaluation = "Overall evaluation currently unavailable"
    else:
        # Mock overall evaluation
        overall_evaluation = (
            "Overall Interview Assessment:\n"
            "1. Performance: Strong technical background with good communication\n"
            "2. Key Strengths: Clear explanations, relevant experience\n"
            "3. Areas for Improvement: Could provide more specific examples\n"
            "4. Role Fit: Good match for technical requirements\n"
            "5. Recommendations: Ready for next stage with minor preparation\n"
        )
        overall_llm_dur = 0.0

    # If no output_path is provided, return a structured JSON result instead of writing files
    total_dur = time.perf_counter() - t_start
    if timings_enabled:
        timings["overall_llm_s"] = round(locals().get("overall_llm_dur", 0.0), 4)
        timings["total_s"] = round(total_dur, 4)

    if output_path is None:
        result: Dict[str, Any] = {
            "generated_at": datetime.now().strftime('%Y-%m-%dT%H:%M:%S'),
            "overall_evaluation": overall_evaluation,
            "details": detailed_analysis,
        }
        if timings_enabled:
            result["timings"] = timings
        return result

    # Generate PDF report (or fallback to plain text if fpdf is unavailable)
    if _HAS_FPDF:
        pdf = FPDF()
        pdf.add_page()
        
        # Add title
        pdf.set_font("Arial", "B", 16)
        pdf.cell(0, 10, "Interview Assessment Report", ln=True, align="C")
        pdf.ln(10)
        
        # Add date
        pdf.set_font("Arial", "", 12)
        pdf.cell(0, 10, f"Date: {datetime.now().strftime('%Y-%m-%d')}", ln=True)
        pdf.ln(10)
        
        # Add overall evaluation
        pdf.set_font("Arial", "B", 14)
        pdf.cell(0, 10, "Overall Evaluation", ln=True)
        pdf.set_font("Arial", "", 12)
        pdf.multi_cell(0, 10, overall_evaluation)
        pdf.ln(10)
        
        # Add detailed analysis for each response
        pdf.set_font("Arial", "B", 14)
        pdf.cell(0, 10, "Detailed Response Analysis", ln=True)
        
        for i, analysis in enumerate(detailed_analysis):
            pdf.set_font("Arial", "B", 12)
            pdf.cell(0, 10, f"Question {i+1}", ln=True)
            pdf.set_font("Arial", "", 12)
            pdf.multi_cell(0, 10, f"Q: {analysis['qa']['question']}")
            pdf.multi_cell(0, 10, f"A: {analysis['qa']['answer']}")
            pdf.multi_cell(0, 10, analysis['assessment'])
            pdf.ln(5)
        
        # Save the PDF
        t_pdf = time.perf_counter()
        pdf.output(output_path)
        pdf_dur = time.perf_counter() - t_pdf
        logger.info(f"PDF report written to {output_path} in {pdf_dur:.3f}s")
        if timings_enabled:
            timings["pdf_write_s"] = round(pdf_dur, 4)
            timings["total_s"] = round(total_dur, 4)
        return output_path
    else:
        # Fallback: write a plain-text report so callers still receive an artifact
        txt_path = os.path.splitext(output_path)[0] + ".txt"
        t_txt = time.perf_counter()
        with open(txt_path, "w", encoding="utf-8") as f:
            f.write("Interview Assessment Report\n")
            f.write(f"Date: {datetime.now().strftime('%Y-%m-%d')}\n\n")
            f.write("Overall Evaluation\n")
            f.write(overall_evaluation + "\n\n")
            f.write("Detailed Response Analysis\n")
            for i, analysis in enumerate(detailed_analysis):
                f.write(f"Question {i+1}\n")
                f.write(f"Q: {analysis['qa']['question']}\n")
                f.write(f"A: {analysis['qa']['answer']}\n")
                f.write(analysis['assessment'] + "\n\n")
        txt_dur = time.perf_counter() - t_txt
        logger.info(f"Text report written to {txt_path} in {txt_dur:.3f}s")
        if timings_enabled:
            timings["text_write_s"] = round(txt_dur, 4)
            timings["total_s"] = round(total_dur, 4)
        return txt_path

def _format_kobert_text(answer_text: str) -> str:
    """Build a compact textual summary from KoBERT emotion + intent for prompts."""
    if not answer_text:
        return "(no answer)"
    emo = _safe_analyze_emotion(answer_text)
    inten = _safe_analyze_intent(answer_text)
    parts: List[str] = []
    if emo:
        ep = emo.get("probs", {})
        parts.append(
            f"Emotion: {emo.get('label','unknown')} (pos={ep.get('positive',0):.2f}, neu={ep.get('neutral',0):.2f}, neg={ep.get('negative',0):.2f})"
        )
    if inten:
        parts.append(f"Intent top: {inten.get('top_label','?')} conf={inten.get('confidence',0):.2f}")
        # include top-3 intents
        dist_items = sorted(inten.get("probs", {}).items(), key=lambda x: x[1], reverse=True)[:3]
        if dist_items:
            parts.append("Intent dist: " + ", ".join(f"{k}={v:.2f}" for k, v in dist_items))
    return "\n".join(parts) if parts else "(no KoBERT analysis)"