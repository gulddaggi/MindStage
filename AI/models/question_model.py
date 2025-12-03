"""Question generation helpers.

This module provides a function to generate interview questions from a
context that contains:

- jd: str
- resume: list[dict]  -- example: [{"1_q": "Question text"}, {"1_a": "Answer text"}, ...]
- qna: list[dict]     -- example: [{"question": "...", "answer": "..."}, ...]

The implementation will attempt to use LangChain + an LLM if available and
configured (e.g. OpenAI via environment variable). If LangChain or an API
key are not available, it falls back to a deterministic, rule-based generator
that produces reasonable starter questions.
"""
from typing import Dict, Any, List, Tuple
import os
import re
from threading import Lock
import time
import logging
import requests
from pathlib import Path

logger = logging.getLogger(__name__)

# Suppress verbose HuggingFace HTTP debug logs
logging.getLogger("urllib3.connectionpool").setLevel(logging.WARNING)
logging.getLogger("transformers.tokenization_utils_base").setLevel(logging.WARNING)

# Normalize credentials and base URL for GMS API gateway
if os.getenv("GMS_KEY") and not os.getenv("OPENAI_API_KEY"):
    os.environ["OPENAI_API_KEY"] = os.environ["GMS_KEY"]
if not os.getenv("OPENAI_API_BASE"):
    os.environ["OPENAI_API_BASE"] = "https://gms.ssafy.io/gmsapi/api.openai.com/v1"

def _korean_ratio(text: str) -> float:
    """Approximate ratio of Korean Hangul characters in text."""
    if not text:
        return 0.0
    hangul = sum(1 for ch in text if '\uac00' <= ch <= '\ud7a3')
    return hangul / max(1, len(text))

# Try importing LangChain; keep module usable even if it's not installed.
_HAS_LANGCHAIN = True
_HAS_CHAT = True
try:
    from langchain.prompts import PromptTemplate
    from langchain.chains import LLMChain
    # Prefer chat models via LangChain for GMS compatibility
    try:
        # Newer unified initializer
        from langchain.chat_models import init_chat_model, ChatOpenAI  # type: ignore
    except Exception:
        # Provider package fallback (for newer LangChain setups)
        try:
            from langchain_openai import ChatOpenAI  # type: ignore
            init_chat_model = None  # type: ignore
        except Exception:
            # Legacy fallback (some versions exposed ChatOpenAI here)
            try:
                from langchain.chat_models import ChatOpenAI  # type: ignore
                init_chat_model = None  # type: ignore
            except Exception:
                _HAS_CHAT = False
    # Keep legacy LLM import available as a last resort (not preferred for GMS)
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

# --- Singleton caches ---
_LLAMA_INSTANCE = None
_LLAMA_LOCK = Lock()
_OPENAI_LLM = None
_OPENAI_LOCK = Lock()
_OPENAI_CHAT = None
_OPENAI_CHAT_LOCK = Lock()

# --- Optional KoBERT analysis (emotion + intent) ---
_KOBERT_ENABLE = os.getenv("KOBERT_ANALYSIS", "true").lower() == "true"
_INTENT_CACHE = {
    "loaded": False,
    "model": None,
    "tokenizer": None,
    "label_map": None,
}
_INTENT_LOCK = Lock()

def _safe_analyze_emotion(text: str) -> Dict[str, Any] | None:
    """Analyze sentiment (positive/neutral/negative) using KoBERT emotion model.

    Returns a dict like {label: str, probs: {positive: float, neutral: float, negative: float}}
    or None on failure/disabled.
    """
    if not _KOBERT_ENABLE or not text:
        try:
            logger.info(
                f"KoBERT emotion skipped: enabled={_KOBERT_ENABLE}, has_text={bool(text)}"
            )
        except Exception:
            pass
        return None
    try:
        # Lazy import to avoid startup cost when disabled
        from . import emotion_model as emo
        # Use sentence splitter for better stability
        sents = emo.split_sentences(text) if hasattr(emo, "split_sentences") else [text]
        t_emo = time.perf_counter()
        sent_results, para = emo.predict_sentences(sents)  # type: ignore
        dur_emo = time.perf_counter() - t_emo
        if not para:
            # Fallback: aggregate from first result
            res = sent_results[0] if sent_results else None
            if not res:
                return None
            # map labels (uncertain -> neutral)
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
        # Paragraph summary path
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

def _ensure_intent_loaded():
    if _INTENT_CACHE["loaded"]:
        return
    with _INTENT_LOCK:
        if _INTENT_CACHE["loaded"]:
            return
        try:
            from . import intent_model as im
            # Default paths in repo
            repo_root = Path(__file__).resolve().parents[2]
            model_path = os.getenv(
                "INTENT_MODEL_PATH",
                str(repo_root / "ai" / "v1_code" / "using_custom_models" / "model_intent_v2_quantized.pt"),
            )
            label_map_path = os.getenv(
                "INTENT_LABEL_MAP_PATH",
                str(repo_root / "ai" / "v1_code" / "using_custom_models" / "label_map.txt"),
            )
            t_load = time.perf_counter()
            tokenizer = im.get_tokenizer()  # type: ignore
            model, label_map = im.load_quantized_model(model_path, label_map_path)  # type: ignore
            load_dur = time.perf_counter() - t_load
            _INTENT_CACHE.update({
                "loaded": True,
                "model": model,
                "tokenizer": tokenizer,
                "label_map": label_map,
            })
            try:
                labels_preview = list(label_map.values()) if isinstance(label_map, dict) else label_map
            except Exception:
                labels_preview = "<unknown labels>"
            logger.info(f"KoBERT intent model loaded for question generation in {load_dur:.3f}s; labels={labels_preview}")
        except Exception as e:
            logger.warning(f"KoBERT intent model load failed: {e}")
            _INTENT_CACHE["loaded"] = False

def _safe_analyze_intent(text: str) -> Dict[str, Any] | None:
    """Analyze intent distribution using KoBERT intent model.

    Returns a dict like {top_label: str, confidence: float, probs: {label: score, ...}}
    or None on failure/disabled.
    
    Note: Now uses analyze_paragraph() for multi-sentence text instead of predict_intent()
    for single sentences, which is more efficient.
    """
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
        im = __import__(__name__.replace("question_model", "intent_model"), fromlist=['*'])
        model = _INTENT_CACHE["model"]
        tok = _INTENT_CACHE["tokenizer"]
        label_map = _INTENT_CACHE["label_map"]
        
        t_int = time.perf_counter()
        # Use analyze_paragraph for full text (handles sentence splitting internally)
        results = im.analyze_paragraph(model, tok, text, label_map)  # type: ignore
        dur_int = time.perf_counter() - t_int
        
        # Aggregate results from all sentences to get overall intent
        if not results or len(results) == 0:
            return None
        
        # If single sentence, use its result directly
        if len(results) == 1:
            res = results[0]
            probs_map = {label_map[i]: float(res['probabilities'][i]) for i in range(len(res['probabilities']))}
            out = {
                "top_label": res['intent'],
                "confidence": float(res['confidence']),
                "probs": probs_map,
                "top2": res['top2']
            }
        else:
            # Multiple sentences: aggregate by averaging probabilities
            num_labels = len(label_map)
            avg_probs = [0.0] * num_labels
            for res in results:
                for i in range(num_labels):
                    avg_probs[i] += res['probabilities'][i]
            avg_probs = [p / len(results) for p in avg_probs]
            
            # Find top label and confidence
            max_idx = avg_probs.index(max(avg_probs))
            pred_label = label_map[max_idx]
            conf = avg_probs[max_idx]
            
            # Build top-2
            sorted_indices = sorted(range(num_labels), key=lambda i: avg_probs[i], reverse=True)
            top2 = [(label_map[i], avg_probs[i]) for i in sorted_indices[:2]]
            
            probs_map = {label_map[i]: float(avg_probs[i]) for i in range(num_labels)}
            out = {"top_label": pred_label, "confidence": float(conf), "probs": probs_map, "top2": top2}
        
        try:
            top3 = sorted(out["probs"].items(), key=lambda x: x[1], reverse=True)[:3]
            logger.info(
                f"KoBERT intent took {dur_int:.3f}s ({len(results)} sentences): top={out['top_label']} conf={out['confidence']:.2f}; top3="
                + ", ".join(f"{k}={v:.2f}" for k, v in top3)
            )
        except Exception:
            pass
        return out
    except Exception as e:
        logger.warning(f"KoBERT intent analysis failed: {e}")
        return None

def _analyze_sentence_with_models(sentence: str) -> Dict[str, Any]:
    """Analyze a single sentence with emotion and intent models.
    
    Returns:
        Dict with keys: sentence, emotion, emotion_score, intent, intent_score, top2_intents
    """
    result = {
        "sentence": sentence,
        "emotion": "neutral",
        "emotion_score": 0.0,
        "intent": "Unknown",
        "intent_score": 0.0,
        "top2_intents": []
    }
    
    # Analyze emotion
    emo = _safe_analyze_emotion(sentence)
    if emo:
        result["emotion"] = emo.get("label", "neutral")
        probs = emo.get("probs", {})
        # Get score for the predicted emotion label
        result["emotion_score"] = float(probs.get(result["emotion"], 0.0))
    
    # Analyze intent
    inten = _safe_analyze_intent(sentence)
    if inten:
        result["intent"] = inten.get("top_label", "Unknown")
        result["intent_score"] = float(inten.get("confidence", 0.0))
        result["top2_intents"] = inten.get("top2", [])
    
    return result


def _annotate_text_with_analysis(text: str) -> tuple[str, List[Dict[str, Any]]]:
    """Annotate text with emotion and intent analysis for each sentence.
    
    Args:
        text: Input text to analyze (can be full paragraph/answer)
        
    Returns:
        Tuple of (annotated_text, analysis_list)
        - annotated_text: Text with inline annotations
        - analysis_list: List of analysis dicts for statistics
        
    Note: This function now passes the full text to analysis functions,
    which internally use KoBERT's sentence splitter for efficiency.
    Pre-splitting sentences was causing redundant BERT calls.
    """
    if not text or not text.strip():
        return "", []
    
    # Pass full text directly - let KoBERT's models handle sentence splitting internally
    # This avoids double-splitting (regex then emo.split_sentences) which caused excessive BERT calls
    analysis = _analyze_sentence_with_models(text)
    
    # The analysis now represents the entire text (not individual sentences)
    # For backward compatibility, wrap in a list
    analysis_list = [analysis]
    
    # Create annotation for the full text
    annotation = f"\\[emotion: {analysis['emotion']}, intent: {analysis['intent']}\\]"
    annotated_text = f"{text.strip()} {annotation}"
    
    return annotated_text, analysis_list


def _get_emotion_statistics(analysis_list: List[Dict[str, Any]]) -> Dict[str, Any]:
    """Calculate emotion statistics from analysis list.
    
    Returns:
        Dict with most_positive, most_negative, average, and sentence texts
    """
    if not analysis_list:
        return {
            "most_positive": 0.0,
            "most_positive_sentence": "",
            "most_negative": 0.0,
            "most_negative_sentence": "",
            "average": 0.0,
            "total_sentences": 0
        }
    
    # Map emotion labels to scores (positive=1, neutral=0, negative=-1)
    emotion_scores = []
    most_pos_idx = 0
    most_neg_idx = 0
    max_pos_score = -1.0
    max_neg_score = 2.0  # Start high for finding minimum
    
    for idx, analysis in enumerate(analysis_list):
        emotion = analysis["emotion"]
        score = analysis["emotion_score"]
        
        # Calculate sentiment value
        if emotion == "positive":
            sentiment = score
        elif emotion == "negative":
            sentiment = -score
        else:  # neutral
            sentiment = 0.0
        
        emotion_scores.append(sentiment)
        
        # Track most positive (highest positive sentiment)
        if emotion == "positive" and score > max_pos_score:
            max_pos_score = score
            most_pos_idx = idx
        
        # Track most negative (highest negative sentiment)
        if emotion == "negative" and score > max_neg_score:
            max_neg_score = score
            most_neg_idx = idx
    
    # Calculate average
    avg_sentiment = sum(emotion_scores) / len(emotion_scores) if emotion_scores else 0.0
    
    # Get actual scores and sentences
    most_pos_score = max_pos_score if max_pos_score >= 0 else 0.0
    most_neg_score = max_neg_score if max_neg_score < 2.0 else 0.0
    
    return {
        "most_positive": most_pos_score,
        "most_positive_sentence": analysis_list[most_pos_idx]["sentence"] if most_pos_score > 0 else "",
        "most_negative": most_neg_score,
        "most_negative_sentence": analysis_list[most_neg_idx]["sentence"] if most_neg_score > 0 else "",
        "average": avg_sentiment,
        "total_sentences": len(analysis_list)
    }


def _format_emotion_statistics(stats: Dict[str, Any]) -> str:
    """Format emotion statistics for inclusion in prompt."""
    if not stats or stats["total_sentences"] == 0:
        return ""
    
    parts = ["Emotion Analysis Summary:"]
    
    if stats["most_positive"] > 0:
        parts.append(f"- Most Positive ({stats['most_positive']:.2f}): \"{stats['most_positive_sentence']}\"")
    
    if stats["most_negative"] > 0:
        parts.append(f"- Most Negative ({stats['most_negative']:.2f}): \"{stats['most_negative_sentence']}\"")
    
    parts.append(f"- Average Sentiment: {stats['average']:.2f}")
    parts.append(f"- Total Sentences: {stats['total_sentences']}")
    
    return "\n".join(parts)


def _get_llama_instance():
    """Lazily initialize and cache a llama-cpp Llama instance."""
    global _LLAMA_INSTANCE
    if _LLAMA_INSTANCE is not None:
        return _LLAMA_INSTANCE
    with _LLAMA_LOCK:
        if _LLAMA_INSTANCE is not None:
            return _LLAMA_INSTANCE
        from pathlib import Path
        from llama_cpp import Llama

        # Resolve model path: prefer env; else try auto-detect Qwen/Yi/Llama/Mistral in common folders
        model_path = os.getenv("LOCAL_LLM_MODEL", "mistral-7b-instruct-v0.1.Q4_K_M.gguf")
        mp = Path(model_path)
        repo_root = Path(__file__).resolve().parents[2]
        if not mp.exists():
            # Try common locations with the exact name
            direct_candidates = [
                repo_root / "models" / mp.name,
                repo_root / "ai" / "model_test" / mp.name,
                repo_root / mp.name,
            ]
            for c in direct_candidates:
                if c.exists():
                    mp = c
                    break
        if not mp.exists():
            # Try to auto-discover a better Korean model (Qwen) or any instruct gguf
            search_dirs = [repo_root / "ai" / "model_test", repo_root / "models", repo_root]
            found = None
            for d in search_dirs:
                try:
                    for f in d.glob("*.gguf"):
                        name = f.name.lower()
                        if "qwen" in name and "instruct" in name:
                            found = f
                            break
                    if found:
                        break
                except Exception:
                    pass
            if found:
                mp = found
            else:
                # Try on-demand download from Hugging Face (first-run only)
                try:
                    from huggingface_hub import hf_hub_download
                    repo_id = os.getenv("HF_GGUF_REPO", "Qwen/Qwen2.5-7B-Instruct-GGUF")
                    filename = os.getenv("HF_GGUF_FILE", "qwen2.5-7b-instruct.Q4_K_M.gguf")
                    local_dir = repo_root / "ai" / "model_test"
                    local_dir.mkdir(parents=True, exist_ok=True)
                    logger.info(f"Downloading {filename} from {repo_id} to {local_dir} (first run may take a while)...")
                    dl_path = hf_hub_download(
                        repo_id=repo_id,
                        filename=filename,
                        local_dir=str(local_dir),
                        local_dir_use_symlinks=False,
                        resume_download=True,
                    )
                    mp = Path(dl_path)
                    logger.info(f"Downloaded model to {mp}")
                except Exception as dl_e:
                    # As a last resort, raise with the original path
                    raise FileNotFoundError(
                        f"Model path does not exist: {model_path}. "
                        "Tried common folders and auto-download failed. "
                        f"Error: {dl_e}. Set LOCAL_LLM_MODEL to an existing .gguf"
                    )

        # Performance tunables via env
        n_ctx = int(os.getenv("LLM_N_CTX", "4096"))
        # Prefer GPU offload by default if available. If LLM_N_GPU_LAYERS is unset,
        # try a large value ("as many as possible"). If GPU init fails, we will
        # automatically retry with CPU only.
        n_gpu_layers_env = os.getenv("LLM_N_GPU_LAYERS")
        if n_gpu_layers_env is not None and n_gpu_layers_env.strip() != "":
            n_gpu_layers = int(n_gpu_layers_env)
        else:
            prefer_gpu = os.getenv("LLM_PREFER_GPU", "true").lower() == "true"
            n_gpu_layers = 999 if prefer_gpu else 0
        n_batch = int(os.getenv("LLM_N_BATCH", "192"))
        n_threads = int(os.getenv("LLM_N_THREADS", "0")) or (os.cpu_count() or 4)

        # Chat format auto-detection with override
        chat_format = os.getenv("LLM_CHAT_FORMAT")
        if not chat_format:
            name = mp.name.lower()
            if "qwen" in name:
                # llama.cpp chat handler expects 'qwen' (not 'qwen2')
                chat_format = "qwen"
            elif "llama-3" in name or "llama3" in name:
                chat_format = "llama-3"
            elif "mistral" in name:
                chat_format = "mistral-instruct"
            elif "yi" in name:
                chat_format = "chatml"
            else:
                chat_format = "mistral-instruct"

        t0 = time.perf_counter()
        try:
            _LLAMA_INSTANCE = Llama(
                model_path=str(mp),
                n_ctx=n_ctx,
                n_gpu_layers=n_gpu_layers,
                n_batch=n_batch,
                n_threads=n_threads,
                chat_format=chat_format,
                verbose=False,
            )
            logger.info(
                f"Loaded GGUF model {mp.name} (ctx={n_ctx}, batch={n_batch}, gpu_layers={n_gpu_layers}, threads={n_threads}, chat_format={chat_format}) in {time.perf_counter() - t0:.3f}s"
            )
        except Exception as gpu_e:
            # If GPU offload failed (e.g., CPU-only build), retry with CPU and warn
            if n_gpu_layers > 0:
                logger.warning(
                    "GPU offload requested (n_gpu_layers=%s) but initialization failed: %s. "
                    "Retrying with CPU-only (n_gpu_layers=0). Consider installing the CUDA-enabled wheel: \n"
                    "  pip install --upgrade --extra-index-url https://abetlen.github.io/llama-cpp-python/ llama-cpp-python-cu12x",
                    n_gpu_layers, gpu_e,
                )
                _LLAMA_INSTANCE = Llama(
                    model_path=str(mp),
                    n_ctx=n_ctx,
                    n_gpu_layers=0,
                    n_batch=n_batch,
                    n_threads=n_threads,
                    chat_format=chat_format,
                    verbose=False,
                )
                logger.info(
                    f"Loaded GGUF model {mp.name} (ctx={n_ctx}, batch={n_batch}, gpu_layers=0, threads={n_threads}, chat_format={chat_format}) in {time.perf_counter() - t0:.3f}s (CPU fallback)"
                )
            else:
                raise
        return _LLAMA_INSTANCE

def _get_openai_llm():
    """Lazily initialize and cache a LangChain OpenAI LLM instance."""
    global _OPENAI_LLM
    if _OPENAI_LLM is not None:
        return _OPENAI_LLM
    with _OPENAI_LOCK:
        if _OPENAI_LLM is not None:
            return _OPENAI_LLM
        if not _HAS_LANGCHAIN:
            return None
        t0 = time.perf_counter()
        # Use GMS API gateway for OpenAI calls (legacy LLM path - not preferred)
        if OpenAI is None:
            logger.warning("Legacy OpenAI LLM class unavailable; prefer chat model path")
            return None
        openai_api_base = os.environ.get("OPENAI_API_BASE", "https://gms.ssafy.io/gmsapi/api.openai.com/v1")
        _OPENAI_LLM = OpenAI(openai_api_base=openai_api_base)
        logger.info(f"Initialized legacy OpenAI LLM with base URL {openai_api_base} in {time.perf_counter() - t0:.3f}s")
        return _OPENAI_LLM

def _get_openai_chat():
    """Lazily initialize and cache a LangChain Chat model configured for GMS.

    Uses init_chat_model('gpt-4o-mini', model_provider='openai') when available,
    otherwise falls back to ChatOpenAI with openai_api_base configured.
    """
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
        t0 = time.perf_counter()
        try:
            # Prefer ChatOpenAI path unless explicitly opting into init_chat_model
            use_init = os.getenv("USE_INIT_CHAT_MODEL", "false").lower() == "true"
            if use_init and 'init_chat_model' in globals() and callable(init_chat_model):  # type: ignore
                try:
                    _OPENAI_CHAT = init_chat_model(model_name, model_provider="openai")  # type: ignore
                    logger.info(f"Initialized chat model via init_chat_model: {model_name}")
                    return _OPENAI_CHAT
                except Exception as e:
                    logger.warning(f"init_chat_model failed, falling back to ChatOpenAI: {e}")
            # Fall back to ChatOpenAI; prefer provider signature (base_url, api_key)
            # Note: GPT-5+ models via GMS only accept default temperature (1)
            api_key = os.getenv("OPENAI_API_KEY") or os.getenv("GMS_KEY")
            try:
                _OPENAI_CHAT = ChatOpenAI(
                    model=model_name, 
                    base_url=base_url, 
                    api_key=api_key,
                    temperature=1
                )  # type: ignore
                logger.info(f"Initialized ChatOpenAI (provider) model: {model_name} with base {base_url}, temperature=1")
            except Exception:
                _OPENAI_CHAT = ChatOpenAI(
                    model=model_name, 
                    openai_api_base=base_url,
                    temperature=1
                )  # type: ignore
                logger.info(f"Initialized ChatOpenAI (legacy) model: {model_name} with base {base_url}, temperature=1")
        except Exception as e:
            logger.warning(f"Failed to initialize chat model: {e}")
            _OPENAI_CHAT = None
        finally:
            logger.info(f"Chat model init took {time.perf_counter() - t0:.3f}s")
        return _OPENAI_CHAT

def _chat_completion_via_gms(messages: List[Dict[str, str]]) -> str:
    """Directly call GMS chat completions endpoint as a fallback when LangChain chat isn't available."""
    api_key = os.getenv("OPENAI_API_KEY") or os.getenv("GMS_KEY")
    if not api_key:
        raise RuntimeError("GMS/OpenAI API key not configured")
    base_url = os.environ.get("OPENAI_API_BASE", "https://gms.ssafy.io/gmsapi/api.openai.com/v1")
    model_name = os.getenv("OPENAI_MODEL", "gpt-4o-mini")
    url = f"{base_url}/chat/completions"
    t0 = time.perf_counter()
    resp = requests.post(
        url,
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        json={
            "model": model_name,
            "messages": messages,
        },
        timeout=(5, 45),
    )
    resp.raise_for_status()
    data = resp.json()
    content = data.get("choices", [{}])[0].get("message", {}).get("content", "")
    logger.info(f"GMS chat completion took {time.perf_counter() - t0:.3f}s")
    return content


def _flatten_resume(resume: List[Dict[str, Any]]) -> tuple[str, List[List[Dict[str, Any]]]]:
    """Convert resume list-of-dicts into annotated text with emotion/intent analysis.

    Expected resume format (flexible):
      [{"1_q": "..."}, {"1_a": "..."}, {"2_q": "..."}, {"2_a": "..."}]
    or
      [{"num": 1, "question": "...", "answer": "..."}, ...]
      
    Returns:
        Tuple of (annotated_text, per_answer_analysis_list)
        - per_answer_analysis_list: [[sentences_for_answer1], [sentences_for_answer2], ...]
    """
    parts: List[str] = []
    per_answer_analysis: List[List[Dict[str, Any]]] = []
    
    # try common shapes
    for item in resume:
        if not isinstance(item, dict):
            continue
        
        # if item contains explicit fields
        if "question" in item and "answer" in item:
            q = item['question']
            a = item['answer']
            # Annotate answer
            annotated_answer, analysis = _annotate_text_with_analysis(a)
            per_answer_analysis.append(analysis)  # Keep structure: one list per answer
            parts.append(f"Q: {q}\nA: {annotated_answer}")
            continue
        
        # if keys encode q/a pair e.g. "1_q" or separate single-key dicts
        for k, v in item.items():
            # If it's an answer field, annotate it
            if k.endswith('_a') or 'answer' in k.lower():
                annotated_v, analysis = _annotate_text_with_analysis(str(v))
                per_answer_analysis.append(analysis)  # Keep structure: one list per answer
                parts.append(f"{k}: {annotated_v}")
            else:
                parts.append(f"{k}: {v}")
    
    return "\n".join(parts), per_answer_analysis


def _estimate_tokens(text: str) -> int:
    """Rough estimation of token count. Not exact but good enough for limiting."""
    # Approximate token count: split on spaces and punctuation
    # This is a rough estimate - real tokenization depends on the model
    words = text.split()
    return len(words) * 1.3  # Add 30% for tokenization overhead

def _select_relevant_qna(qna: List[Dict[str, Any]], max_items: int = 8, max_tokens: int = 1800) -> List[Dict[str, Any]]:
    """
    Select most relevant QnA pairs while staying under token limit.
    Strategies:
    1. Always keep the most recent N questions
    2. Keep questions until we hit token limit
    """
    if not qna:
        return []
    
    # Always include the last 2 QnA pairs for immediate context
    recent_context = qna[-2:] if len(qna) > 2 else qna
    remaining_slots = max_items - len(recent_context)
    
    if remaining_slots <= 0:
        return recent_context
    
    # For remaining slots, select earlier QnA pairs
    # but check token count
    selected = list(recent_context)  # Make a copy
    current_tokens = sum(_estimate_tokens(f"{q['question']}\n{q['answer']}")
                        for q in selected)
    
    # Go through earlier QnA pairs (in reverse order to prefer recent ones)
    for item in reversed(qna[:-2]):  # Skip the last 2 we already included
        q_tokens = _estimate_tokens(f"{item['question']}\n{item['answer']}")
        
        # Check if adding this QnA would exceed our limits
        if (len(selected) >= max_items or 
            current_tokens + q_tokens > max_tokens):
            break
            
        selected.insert(0, item)  # Add at beginning to maintain order
        current_tokens += q_tokens
    
    return selected

def _flatten_qna(qna: List[Dict[str, Any]], max_items: int = 8, max_tokens: int = 1800, annotate_only_latest: bool = False) -> tuple[str, List[Dict[str, Any]]]:
    """
    Convert QnA history to annotated text with emotion/intent analysis, limiting size to control costs.
    Args:
        qna: List of QnA dictionaries
        max_items: Maximum number of QnA pairs to include
        max_tokens: Approximate maximum tokens to include
        
    Returns:
        Tuple of (annotated_text, analysis_list)
    """
    # First select relevant QnA pairs
    selected_qna = _select_relevant_qna(qna, max_items, max_tokens)
    
    # Then format and annotate them
    parts: List[str] = []
    all_analysis: List[Dict[str, Any]] = []
    
    for idx, item in enumerate(selected_qna):
        if not isinstance(item, dict):
            continue
        q = item.get("question") or item.get("q") or list(item.keys())[0]
        a = item.get("answer") or item.get("a") or item.get(q)
        
        # Decide whether to annotate this answer
        if annotate_only_latest and idx != len(selected_qna) - 1:
            # Older QnA: include plain text without invoking models
            parts.append(f"Q: {q}\nA: {str(a).strip()}")
        else:
            # Annotate (either all items, or only the latest one)
            annotated_answer, analysis = _annotate_text_with_analysis(str(a))
            all_analysis.extend(analysis)
            parts.append(f"Q: {q}\nA: {annotated_answer}")
    
    result = "\n".join(parts)
    
    # If we have omitted some QnA pairs, add a note
    if len(selected_qna) < len(qna):
        omitted = len(qna) - len(selected_qna)
        result = f"[{omitted} earlier questions omitted for brevity]\n\n" + result
    
    return result, all_analysis


def _parse_numbered_list(text: str) -> List[str]:
    """Parse a numbered or line-separated output into a list of questions."""
    # split on lines that look like '1.' or '1)'
    lines = [ln.strip() for ln in text.splitlines() if ln.strip()]
    items: List[str] = []
    buf: List[str] = []
    for ln in lines:
        if re.match(r"^\d+\s*[\.|)]\s*", ln):
            if buf:
                items.append(" ".join(buf).strip())
                buf = []
            # remove leading numbering
            ln_clean = re.sub(r"^\d+\s*[\.|)]\s*", "", ln)
            buf.append(ln_clean)
        else:
            # treat as continuation
            buf.append(ln)
    if buf:
        items.append(" ".join(buf).strip())
    # if no numbered bullets detected, fallback to splitting by lines
    if not items and lines:
        return lines
    return items


def _deduplicate_questions(questions: List[str], limit: int) -> List[str]:
    """Deduplicate questions by normalized text and cap to limit.

    Normalization: lowercase + collapse whitespace + strip.
    """
    seen = set()
    unique: List[str] = []
    for q in questions:
        if not q:
            continue
        norm = re.sub(r"\s+", " ", q).strip().lower()
        if norm in seen:
            continue
        seen.add(norm)
        unique.append(q.strip())
        if len(unique) >= limit:
            break
    return unique


def _ensure_polite_questions(questions: List[str]) -> List[str]:
    """Ensure questions end with polite Korean endings; rewrite common casual forms.

    Heuristics only: keeps existing polite endings; otherwise converts imperative/flat tone
    to polite requests or questions. Designed to be conservative.
    """
    if not questions:
        return questions

    allowed_re = re.compile(r"(요[.?!]*$|세요[.?!]*$|습니까\?$|습니까[.?!]*$|주시겠어요[.?!]*$|주실 수 있나요[.?!]*$|하시겠어요[.?!]*$|하실 수 있나요[.?!]*$|인지 말씀해 주세요[.?!]*$)")
    casual_end_re = re.compile(r"(해라|하라|해봐|말해봐|얘기해봐|설명해봐|해|한다|한다\?|다[.?!]*$|하자$)")

    def politeify(s: str) -> str:
        t = (s or "").strip()
        if not t:
            return t
        # If already polite, keep
        if allowed_re.search(t):
            return t
        # Fix common casual imperatives
        t = re.sub(r"(말해봐|얘기해봐|설명해봐|말해)$", "말씀해 주세요", t)
        t = re.sub(r"(해봐)$", "해 주세요", t)
        t = re.sub(r"(해라|하라)$", "해 주세요", t)
        # Replace declarative '...다' with polite request
        t = re.sub(r"다[.?!]*$", "인지 말씀해 주세요.", t)
        # If ends with casual '해', make it a request
        t = re.sub(r"해$", "해 주세요", t)
        # If it is a question without polite ending, make it '~요?'
        if t.endswith("?") and not allowed_re.search(t):
            if t.endswith("요?") or t.endswith("세요?") or t.endswith("습니까?"):
                return t
            # Append 요 before ? if possible
            if not t.endswith("요?"):
                t = t[:-1] + "요?"
        # If no punctuation and not polite, append a polite request
        if not allowed_re.search(t):
            if t.endswith("요") or t.endswith("세요") or t.endswith("습니까"):
                return t + "."
            # default polite request
            if t.endswith("해 주세요") or t.endswith("말씀해 주세요"):
                return t + "."
            t = t.rstrip(".?!") + " 말씀해 주세요."
        return t

    return [politeify(q) for q in questions]


def _parse_interviewer_types_output(text: str, n: int) -> List[int]:
    """Parse a model output into a list of 0/1 types of length n.
    Accepts formats like "0 1 1 0", "0,1,1,0", or lines with digits.
    Fills missing with 1 (friendly) if fewer than n parsed.
    """
    # Find all 0/1 digits in order
    digits = re.findall(r"[01]", text)
    types: List[int] = [int(d) for d in digits[:n]]
    while len(types) < n:
        types.append(1)
    return types


def determine_interviewer_types(questions: List[str], jd: str, qna_history: List[Dict[str, Any]] | None = None) -> List[int]:
    """
    Determine interviewer types for multiple questions at once.
    Returns a list of length len(questions) with 0=strict, 1=friendly.
    """
    if not questions:
        return []

    # Fast heuristic path if no LLM configured
    if not _HAS_LANGCHAIN or not os.getenv("OPENAI_API_KEY"):
        types: List[int] = []
        for q in questions:
            ql = (q or "").lower()
            if any(w in ql for w in ["technical", "explain", "how did you", "performance", "challenging"]):
                types.append(0)
            else:
                types.append(1)
        return types

    # Prepare context
    history_text = ""
    if qna_history:
        last_qa = qna_history[-1]
        history_text = f"Last answer: {last_qa.get('answer', '')}"

    # Try LangChain chat first
    llm = _get_openai_chat()
    if llm is not None:
        try:
            # Avoid importing langchain message classes; pass raw strings instead if supported
            sys_msg = (
                "You output only a sequence of 0/1 digits (no words). Each digit corresponds to the interviewer style for the numbered question in order. "
                "0=strict, 1=friendly. Output exactly N digits separated by spaces or commas."
            )
            q_block = "\n".join(f"{i+1}) {q}" for i, q in enumerate(questions))
            jd_block = jd if (jd and jd.strip()) else "[JD not provided]"
            user_msg = (
                f"Job Description:\n{jd_block}\n\n"
                f"Previous responses:\n{history_text}\n\n"
                f"Questions ({len(questions)}):\n{q_block}\n\n"
                f"Respond with exactly {len(questions)} digits (0 or 1) in order."
            )
            # Use _chat_completion_via_gms-compatible format even when llm is available, for consistency
            out = _chat_completion_via_gms([
                {"role": "system", "content": sys_msg},
                {"role": "user", "content": user_msg},
            ])
            return _parse_interviewer_types_output(out, len(questions))
        except Exception as e:
            logger.debug(f"LangChain batch interviewer type failed: {e}")

    # Fallback to GMS direct chat
    try:
        sys_msg = (
            "You output only a sequence of 0/1 digits (no extra text). Each digit corresponds to the interviewer style for the numbered question in order. "
            "0=strict, 1=friendly. Output exactly N digits separated by spaces or commas."
        )
        q_block = "\n".join(f"{i+1}) {q}" for i, q in enumerate(questions))
        jd_block = jd if (jd and jd.strip()) else "[JD not provided]"
        user_msg = (
            f"Job Description:\n{jd_block}\n\n"
            f"Previous responses:\n{history_text}\n\n"
            f"Questions ({len(questions)}):\n{q_block}\n\n"
            f"Respond with exactly {len(questions)} digits (0 or 1) in order."
        )
        out = _chat_completion_via_gms([
            {"role": "system", "content": sys_msg},
            {"role": "user", "content": user_msg},
        ])
        return _parse_interviewer_types_output(out, len(questions))
    except Exception as e:
        logger.warning(f"Batch interviewer types via GMS failed: {e}")
        # Final fallback: heuristic per question
        types: List[int] = []
        for q in questions:
            ql = (q or "").lower()
            types.append(0 if any(w in ql for w in ["technical", "explain", "how did you", "performance", "challenging"]) else 1)
        return types

def _build_initial_question_prompts(jd_text: str, resume_text: str, n_questions: int) -> Tuple[str, str]:
    """Build system & user prompt for initial (s1) question generation.

    Updated to be role-agnostic, Korean-first (terms can be English), junior-level by default,
    and to prefer concepts mentioned in the resume while grounding in JD.
    """
    system_prompt = (
        "당신은 다양한 직무(IT/비IT 포함)의 면접을 진행하는 전문 면접관입니다. \n"
        "항상 한국어로 질문하되, 기술/전문 용어(예: API, TCP, IFRS, ROI, ERP 등)는 영어 그대로 표기합니다. \n"
        "질문은 번호 목록(1., 2., 3., …) 한 줄 1문항, 각 문항은 1~2개의 짧은 문장으로 된 자연스러운 구어체(존댓말)여야 합니다. 물음표는 선택 사항입니다.\n\n"
        "[원칙]\n"
        "- JD와 이력서를 함께 반영해 직무/도메인을 스스로 추론하고, 신입(주니어) 난이도로 질문합니다.\n"
        "- 이력서에서 언급된 개념/도구/도메인(예: 특정 프레임워크/분야/방법론)을 우선 탐색하되, JD 요구와 균형을 맞춥니다.\n"
        "- 추상적 개념·원리·의사결정 기준을 중심으로, 실무 상황/사례 기반 질문을 섞되 과도한 도구 나열은 지양합니다.\n"
        "- 문어체 요청어휘 금지: '적어주세요/작성해 주세요/기재해 주세요/기술해 주세요/입력해 주세요' 대신 '말씀해 주세요/말해 주세요/알려주세요' 사용.\n"
        "- NCS 역량 5개를 가능하면 고르게 분포: 직무수행능력(Job_Competency), 의사소통(Communication), 팀워크/리더십(Teamwork_Leadership), 윤리성(Integrity), 적응력(Adaptability).\n"
        "- 그룹 호칭(‘여러분/모두’) 금지, 한 명의 지원자만 대상으로 합니다.\n"
        "- 예/아니오로 끝나는 폐쇄형은 최소화하고, 판단/설명/선택 근거를 유도합니다.\n"
        "- 반말 및 서술체(~다) 금지. 반드시 존댓말 종결(~요/~세요/~주시겠어요?/~하실 수 있나요?/~인가요?/~습니까?)만 사용.\n"
        "  금지 예: '~해', '~한다', '~해라', '~하자', '~해봐'. 허용 예: '~해 주세요', '~하시겠어요?', '~하실 수 있나요?', '~인지 말씀해 주세요?', '~습니까?'"
    )
    user_prompt = (
        "아래 Job Description(JD)과 이력서를 바탕으로, 신입(주니어) 기준의 초기 면접 질문 {n}개를 생성하세요.\n\n"
        "Job Description:\n{jd}\n\n"
        "Resume:\n{resume}\n\n"
        "[요구사항]\n"
        "1) 각 문항은 하나의 명확한 포인트만 탐색합니다.\n"
        "2) 이력서에 등장한 개념/도구/도메인을 우선적으로 묻되, JD 핵심 요구와의 연결성을 분명히 하세요.\n"
        "3) 구어체(존댓말), 1~2문장/항목, 150자 이내. 출력은 번호 + 문장만.\n"
        "4) 가능한 한 NCS 5개 역량이 고르게 분포되도록 전체 세트를 구성합니다.\n"
        "5) 자소서/이력서에 이미 서술된 문장을 그대로 다시 요구하거나 재진술하지 마세요. 그 내용에서 파생된 새로운 관점의 질문으로 바꾸세요.\n"
        "6) 이번 세트 내부에서도 질문 간 주제·표현이 중복되지 않도록 서로 다른 포인트를 다루세요."
    )
    formatted_user = user_prompt.format(n=n_questions, jd=jd_text, resume=resume_text)
    return system_prompt, formatted_user

def _build_tail_question_prompts(jd_text: str, resume_text: str, qna_text: str, n_questions: int) -> Tuple[str, str]:
    """Build system & user prompt for tail (follow-up, s2) question generation.

    Updated to keep Korean + English terms rule, junior default, and resume-preference.
    """
    system_prompt = (
        "당신은 다양한 직무의 꼬리질문을 능숙하게 진행하는 전문 면접관입니다. \n"
        "항상 한국어로 질문하되, 기술/전문 용어는 영어 표기를 유지합니다. \n"
        "최신 답변 문맥을 활용해 심화/명확화 또는 부족 역량 전환을 수행하세요.\n\n"
        "[원칙]\n"
        "- JD와 이력서를 함께 고려하되, 이력서에 언급된 개념/도구/도메인을 우선적으로 파고듭니다(신입 기준 난이도).\n"
        "- 동일 주제 반복은 피하고, 관점을 바꾸거나(가정/작은 단계/경험 회상) 부족한 NCS 역량으로 전환합니다.\n"
        "- 구어체(존댓말, 말씀해 주세요/말해 주세요/알려주세요’) 사용.\n"
        "- 그룹 호칭 금지, 한 명의 지원자만 대상으로 합니다.\n"
        "- 반말 및 서술체(~다) 금지. 반드시 존댓말 종결(~요/~세요/~주시겠어요?/~하실 수 있나요?/~인가요?/~습니까?)만 사용.\n"
        "  금지 예: '~해', '~한다', '~해라', '~하자', '~해봐'. 허용 예: '~해 주세요', '~하시겠어요?', '~하실 수 있나요?', '~인지 말씀해 주세요?', '~습니까?'"
    )
    adaptive_guidelines = (
        "- 답변이 모호/부족/'잘 모르겠습니다' 유형이면: 첫 질문에서만 아주 짧게(<=12자) 언급 후 쉬운 관점(기초 개념, 작은 단계, 최근 시도, 단일 사례)으로 재질문\n"
        "- 동일 주제 확장 시 단순 재표현 금지; 관점 변경 (가정 시나리오, 작은 활동 단계, 경험 회상) 활용\n"
        "- 매우 짧은 답변이면 구체 요소(역할, 단계, 어려움 중 1~2개)만 선택해 깊이 질문\n"
        "- 부정·불확실 직후 공격적 추궁 금지; 작은 학습/의도 드러낼 기회 제공\n"
        "- 각 질문은 두 전략 중 하나만 선택: (확장/명확화) 또는 (주제 전환/다른 역량). 라벨이나 전략명은 출력에 포함하지 말 것\n"
        "- 가능하면 생성된 질문 집합 안에 최소 1개 이상 '주제 전환/다른 역량' 유형 포함"
    )
    user_prompt = (
        "아래 정보를 참고해 꼬리(후속) 면접 질문 {n}개를 생성하세요.\n\n"
        "Job Description:\n{jd}\n\n"
        "Resume:\n{resume}\n\n"
        "Conversation Context:\n{qna}\n\n"
        "[요구사항]\n"
        "1) 최신 답변과 이력서의 용어/경험을 연결해 자연스럽게 이어가되, 한 번에 한 방향만 깊게 파고듭니다.\n"
        "2) 이미 다룬 주제를 재진술하지 말고, 관점을 바꾸거나 부족한 NCS 역량(직무수행/의사소통/팀워크·리더십/윤리성/적응력)으로 전환합니다.\n"
        "3) 구어체(존댓말), 문어체 요청어휘 금지, 항목당 1~2문장, 150자 이내, 번호만 출력.\n"
        "4) 가능한 한 이력서에 언급된 개념/도구/도메인을 우선적으로 탐색하십시오.\n"
        "5) 이전에 제시된 질문(Conversation Context 포함)과 동일/유사한 질문은 생성하지 마세요. 단순 재표현은 금지합니다.\n"
        "6) 이번 세트 내부에서도 질문 간 중복을 피하고, 서로 다른 포인트를 다루세요.\n"
        "[적응형 지침]\n{adaptive}"
    )
    formatted_user = user_prompt.format(n=n_questions, jd=jd_text, resume=resume_text, qna=qna_text, adaptive=adaptive_guidelines)
    return system_prompt, formatted_user

def generate_questions_from_context(context: Dict[str, Any], n_questions: int = 3) -> List[str]:
    """
    Generate interview questions from the provided context.

    Parameters
    - context: dict with keys: 'jd' (str), 'resume' (list[dict]), 'qna' (list[dict])
    - n_questions: how many questions to generate

    Returns: list of question strings (always)

    Notes: If LangChain + an LLM is available and configured, this will call
    the LLM. Otherwise the function returns a set of heuristic questions based
    on keywords from the JD and resume.
    """
    jd_text = context.get("jd") or context.get("jd_text") or ""
    resume = context.get("resume") or context.get("resume_text") or []
    qna = context.get("qna") or context.get("qna_history") or []

    # Track total BERT analysis time for this request
    t_bert_start = time.perf_counter()

    # Only analyze resume if we have NO QnA history (i.e., s1 - start of interview)
    # In s2 (during interview), skip resume analysis to save processing time
    if not qna or len(qna) == 0:
        # s1: analyze resume since we don't have interview responses yet
        resume_text, resume_analysis = (resume, []) if isinstance(resume, str) else _flatten_resume(resume)
    else:
        # s2: skip resume analysis, just format as plain text (no BERT calls)
        if isinstance(resume, str):
            resume_text, resume_analysis = resume, []
        else:
            # Simple text formatting without analysis
            parts = []
            for item in resume:
                if isinstance(item, dict):
                    if "question" in item and "answer" in item:
                        parts.append(f"Q: {item['question']}\nA: {item['answer']}")
                    else:
                        for k, v in item.items():
                            parts.append(f"{k}: {v}")
            resume_text, resume_analysis = "\n".join(parts), []
    
    # Build QnA text:
    # s1 (no QnA): qna_text empty
    # s2 (has QnA): include ONLY previous questions (no answers) + latest Q/A with annotated latest answer
    if not qna or len(qna) == 0:
        qna_text = ""
        qna_analysis: List[Dict[str, Any]] = []
    else:
        if isinstance(qna, str):
            # Unexpected shape; treat as opaque text (no extra analysis to avoid cost)
            qna_text = qna[:5000]
            qna_analysis = []
        else:
            # Previous questions (exclude last item) – no answers to reduce redundancy & cost
            prev_questions: List[str] = []
            for item in qna[:-1]:
                if isinstance(item, dict):
                    q = item.get("question") or item.get("q")
                    if q:
                        prev_questions.append(str(q).strip())
            latest = qna[-1] if isinstance(qna[-1], dict) else {}
            latest_question = (latest.get("question") or latest.get("q") or "").strip()
            latest_answer_raw = (latest.get("answer") or latest.get("a") or "").strip()
            annotated_latest_answer = latest_answer_raw
            latest_analysis: List[Dict[str, Any]] = []
            if latest_answer_raw:
                # Single KoBERT call for latest answer only
                annotated_latest_answer, latest_analysis = _annotate_text_with_analysis(latest_answer_raw)
            # Compose prompt section
            parts: List[str] = []
            if prev_questions:
                parts.append("[이전 질문 목록]")
                for pq in prev_questions[-15:]:  # safety cap
                    parts.append(f"Q: {pq}")
            parts.append("\n[최신 질답]")
            if latest_question:
                parts.append(f"Q: {latest_question}")
            if latest_answer_raw:
                parts.append(f"A: {annotated_latest_answer}")
            qna_text = "\n".join(parts)
            qna_analysis = latest_analysis

    # Log total BERT analysis time
    bert_total_time = time.perf_counter() - t_bert_start
    logger.info(f"Total BERT analysis time: {bert_total_time:.3f}s")

    # Limit prompt size to accelerate encoding (lower default)
    max_input_chars = int(os.getenv("LLM_MAX_INPUT_CHARS", "5000"))
    if max_input_chars > 0:
        jd_text = jd_text[:max_input_chars]
        resume_text = str(resume_text)[:max_input_chars]
        qna_text = str(qna_text)[:max_input_chars]

    # Note: Removed kobert_hint statistics and markers to reduce runtime and prompt size
    if not qna or len(qna) == 0:
        system_prompt, formatted_user = _build_initial_question_prompts(jd_text, resume_text, n_questions)
    else:
        system_prompt, formatted_user = _build_tail_question_prompts(jd_text, resume_text, qna_text, n_questions)

    # --- Enhanced logging with LOG_ANNOTATED control ---
    try:
        qna_len = len(qna) if isinstance(qna, list) else 0
        logger.info(
            "QG: prompt prepared (n=%s, jd_chars=%s, resume_chars=%s, qna_pairs=%s, qna_chars=%s, mode=%s)",
            n_questions, len(jd_text), len(str(resume_text)), qna_len, len(str(qna_text)), "initial" if qna_len == 0 else "tail"
        )
        
        # Single env var for annotated text logging
        if os.getenv("LOG_ANNOTATED", "false").lower() == "true":
            max_chars = 2000
            
            # Log annotated resume
            if resume_text:
                resume_preview = str(resume_text)[:max_chars]
                if len(str(resume_text)) > max_chars:
                    resume_preview += "\n...[truncated]"
                logger.info("=== ANNOTATED RESUME ===\n%s", resume_preview)
            
            # Log annotated QnA history
            if qna_text:
                qna_preview = str(qna_text)[:max_chars]
                if len(str(qna_text)) > max_chars:
                    qna_preview += "\n...[truncated]"
                logger.info("=== ANNOTATED QNA HISTORY ===\n%s", qna_preview)
            
            # Emotion statistics logging removed to reduce overhead
        
        # Optional full prompt logging (guarded by env var)
        if os.getenv("LOG_QUESTION_PROMPT", "false").lower() == "true":
            # Truncate extremely large prompts to avoid log blowup
            max_log = int(os.getenv("LOG_QUESTION_PROMPT_MAX_CHARS", "4000"))
            full_prompt = f"[System]\n{system_prompt}\n\n[User]\n{formatted_user}"
            to_log = full_prompt if len(full_prompt) <= max_log else (full_prompt[:max_log] + "\n...[truncated]...")
            logger.info("=== FULL QG PROMPT ===\n%s", to_log)
        elif not os.getenv("LOG_ANNOTATED", "false").lower() == "true":
            # Provide minimal preview if neither logging is enabled
            qna_preview = str(qna_text)[:500].replace("\n", " ")
            logger.info("QG: QnA preview (first 500 chars): %r%s",
                        qna_preview, " ..." if len(str(qna_text)) > 500 else "")
    except Exception as e:
        # Logging should never break generation; log error but continue
        logger.warning("Logging failed: %s", e)

    # If the environment requests a local LLM, use a local GGUF model via llama-cpp
    use_local = os.getenv("USE_LOCAL_LLM", "false").lower() == "true"
    if use_local:
        try:
            llm = _get_llama_instance()

            # Build chat-style prompts for instruction-tuned models
            system_prompt = (
                "당신은 다양한 직무의 면접을 진행하는 전문 면접관입니다. 한국어로 질문하되, 기술/전문 용어는 영어 표기를 유지합니다.\n"
                "규칙:\n"
                "- 출력은 1부터 시작하는 번호 목록만 제공합니다(1., 2., …).\n"
                "- 한 줄에 한 항목만 작성하며, 항목은 1~2개의 짧은 문장으로 구성된 구어체(존댓말)로 만듭니다.\n"
                "- 서론/결론/설명/면책 조항/날짜 언급은 금지합니다. 문장 끝 '?'는 선택입니다.\n"
                "- 신입(주니어) 기준 난이도, JD와 이력서를 함께 반영합니다.\n"
                "- 이력서에 언급된 개념/도구/도메인을 우선 탐색하되 JD 핵심 요구와 연결합니다.\n"
                "- 5가지 NCS 역량(직무수행/의사소통/팀워크·리더십/윤리성/적응력)을 고르게 다룹니다.\n"
            )
            user_prompt = (
                f"Job Description (JD):\n{jd_text}\n\n"
                f"Resume:\n{resume_text}\n\n"
                f"Previous Q&A (recent first):\n{qna_text}\n\n"
                f"위 정보를 반영해 질문 {n_questions}개를 생성하세요. 질문들이 서로 다른 NCS 역량을 고르게 다루도록 구성하십시오."
            )

            messages = [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ]

            # Use llama-cpp chat completion for better adherence to instruction format
            t_call = time.perf_counter()
            # Scale output tokens by requested questions with upper bound
            max_out = int(os.getenv("LLM_MAX_TOKENS_PER_QUESTION", "48")) * max(1, n_questions)
            max_out = min(max(32, max_out), int(os.getenv("LLM_MAX_TOKENS_CAP", "256")))
            result = llm.create_chat_completion(
                messages=messages,
                temperature=0.7,
                max_tokens=max_out,
                top_p=0.95,
                stop=["</s>"]
            )
            logger.info(f"llama-cpp chat completion took {time.perf_counter() - t_call:.3f}s")
            text = result["choices"][0]["message"]["content"] if isinstance(result, dict) else ""
            # If the model did not produce enough Korean, retry once with stricter instructions
            if _korean_ratio(text) < 0.3:
                strict_messages = [
                    {"role": "system", "content": system_prompt + "\n(주의: 반드시 한국어로만 작성)"},
                    {"role": "user", "content": user_prompt + "\n출력은 한국어만 사용하세요."},
                ]
                t_call2 = time.perf_counter()
                result2 = llm.create_chat_completion(
                    messages=strict_messages,
                    temperature=0.3,
                    max_tokens=max_out,
                    top_p=0.9,
                    stop=["</s>"]
                )
                logger.info(f"llama-cpp retry took {time.perf_counter() - t_call2:.3f}s")
                text2 = result2["choices"][0]["message"]["content"] if isinstance(result2, dict) else ""
                if _korean_ratio(text2) >= _korean_ratio(text):
                    text = text2
            questions = _parse_numbered_list(text)
            questions = _ensure_polite_questions(questions)
            return _deduplicate_questions(questions, n_questions)
        except Exception as e:
            # If transformers isn't available or model fails, log and fall back
            logger.warning(f"Local LLM generation failed: {e}")

    # Only use OpenAI/LangChain if not in local LLM mode
    if not use_local and _HAS_LANGCHAIN and os.getenv("OPENAI_API_KEY"):
        try:
            # Attempt to import LangChain message classes; if unavailable, skip to GMS fallback
            try:
                from langchain_core.messages import SystemMessage, HumanMessage  # type: ignore
            except ImportError:
                SystemMessage = HumanMessage = None  # type: ignore
            llm = _get_openai_chat()
            if llm is not None and SystemMessage and HumanMessage:
                messages = [
                    SystemMessage(content=system_prompt),
                    HumanMessage(content=formatted_user)
                ]
                t_openai = time.perf_counter()
                response = llm.invoke(messages)
                resp_text = response.content if hasattr(response, 'content') else str(response)
                logger.info(f"OpenAI chat generation took {time.perf_counter() - t_openai:.3f}s")
                questions = _parse_numbered_list(resp_text)
                questions = _ensure_polite_questions(questions)
                return _deduplicate_questions(questions, n_questions)
            # Direct GMS fallback using the structured prompts
            resp_text = _chat_completion_via_gms([
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": formatted_user},
            ])
            questions = _parse_numbered_list(resp_text)
            questions = _ensure_polite_questions(questions)
            return _deduplicate_questions(questions, n_questions)
        except Exception as e:
            logger.warning(f"LangChain/LLM generation failed: {e}")

    # Fallback deterministic generator: pick keywords and produce NCS-aligned templates
    # simple keyword extraction from jd and resume
    combined = (jd_text + " " + resume_text).lower()
    keywords = []
    for tok in ["python", "sql", "team", "lead", "aws", "ml", "react", "docker", "k8s"]:
        if tok in combined and tok not in keywords:
            keywords.append(tok)

    # Generate questions mapped to NCS competencies
    templates = []
    
    # 1. Job_Competency (직무수행능력) - Technical/domain-specific questions
    if "python" in keywords:
        templates.append("파이썬을 사용한 프로젝트에서 가장 어려웠던 기술적 문제와 해결 방법을 설명해주세요.")
        
    if "sql" in keywords:
        templates.append("데이터베이스 설계나 쿼리 최적화와 관련하여 해결했던 복잡한 문제를 설명해주세요.")
        
    if "aws" in keywords:
        templates.append("AWS 서비스를 활용한 경험이 있다면, 어떤 서비스를 사용했고 왜 선택했는지 설명해주세요.")
        
    if "ml" in keywords:
        templates.append("머신러닝 프로젝트에서 모델 성능을 어떻게 검증하고 개선했는지 설명해주세요.")
        
    if "react" in keywords:
        templates.append("React 애플리케이션을 유지보수 가능하고 성능 좋게 구조화하는 방법을 설명해주세요.")
        
    if "docker" in keywords or "k8s" in keywords:
        templates.append("컨테이너화된 애플리케이션을 프로덕션에서 운영한 경험을 설명해주세요.")
        
    # 2. Communication (의사소통능력)
    templates.append("비기술 팀원에게 복잡한 기술적 개념을 설명해야 했던 경험을 공유해주세요.")
    
    # 3. Teamwork_Leadership (팀워크 및 리더십)
    if "team" in keywords or "lead" in keywords:
        templates.append("팀 내 갈등 상황을 어떻게 해결했고, 프로젝트를 성공적으로 완수했는지 사례를 공유해주세요.")
    else:
        templates.append("팀 프로젝트에서 의견 충돌이 있었을 때 어떻게 협력하여 해결했는지 말씀해주세요.")
    
    # 4. Integrity (도덕성/윤리성)
    templates.append("업무 중 윤리적 딜레마나 어려운 결정을 내려야 했던 경험이 있다면 공유해주세요.")
    
    # 5. Adaptability (적응력/유연성)
    templates.append("익숙하지 않은 새로운 기술을 빠르게 학습하고 적용해야 했던 경험을 설명해주세요.")
    
    # If not enough templates, add more diverse NCS questions
    additional_ncs_questions = [
        "프로젝트 일정이 촉박한 상황에서 스트레스를 어떻게 관리하셨나요?",  # Adaptability
        "다른 부서와 협업하여 목표를 달성했던 경험을 말씀해주세요.",  # Teamwork_Leadership
        "고객이나 동료의 피드백을 받고 업무 방식을 개선한 사례가 있나요?",  # Communication
        "이 직무에 지원하게 된 동기와 본인의 강점이 어떻게 부합하는지 설명해주세요.",  # Job_Competency
    ]
    
    while len(templates) < n_questions:
        templates.extend(additional_ncs_questions)
        

    # Return questions only (consistent return type)
    return _deduplicate_questions(_ensure_polite_questions(templates), n_questions)


if __name__ == "__main__":
    # Example usage matching the context shape you described
    example_context = {
        "jd": "Software engineer role requiring Python, SQL, teamwork and AWS",
        "resume": [
            {"1_q": "Worked on backend services using Python"},
            {"1_a": "Built REST APIs and ETL pipelines"},
            {"2_q": "Experience with databases"},
            {"2_a": "Designed schemas and optimized queries"}
        ],
        "qna": [
            {"question": "Tell me about your last project.", "answer": "I built a data pipeline."}
        ]
    }
    questions = generate_questions_from_context(example_context, n_questions=4)
    print("Generated questions:")
    for q in questions:
        print(q)

