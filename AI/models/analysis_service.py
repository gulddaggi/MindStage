"""Async analysis service for interview end (s3) endpoint.

Computes:
1. Intent scores (5 NCS competencies) averaged across all sentences
2. Emotion labels for each sentence (uncertain=0, positive=1, negative=2)
3. Overall report text combining scores, labels, and LLM analysis

This version is async-safe:
 - Models are loaded once and cached globally
 - Loading and inference run in thread pool executors to avoid blocking the event loop
 - Multiple concurrent requests share the same loaded models safely
"""
import logging
from typing import Dict, Any, List, Tuple
from pathlib import Path
import os
import asyncio
from concurrent.futures import ThreadPoolExecutor

logger = logging.getLogger(__name__)

# Global cache & lock for async-safe lazy loading
_MODEL_CACHE: Dict[str, Any] = {}
_MODEL_LOCK = asyncio.Lock()
_EXECUTOR: ThreadPoolExecutor | None = None

def _get_executor() -> ThreadPoolExecutor:
    global _EXECUTOR
    if _EXECUTOR is None:
        # Create a small pool; quantized models are light CPU tasks
        _EXECUTOR = ThreadPoolExecutor(max_workers=int(os.getenv("ANALYSIS_WORKERS", "4")))
    return _EXECUTOR

def _load_models_sync() -> Dict[str, Any]:
    """Synchronous part: import and load models + tokenizer.
    Returns a dict containing intent_model, emotion_model module refs, tokenizer, label_map.
    """
    from . import intent_model as im
    from . import emotion_model as emo
    repo_root = Path(__file__).resolve().parents[2]
    intent_model_path = os.getenv(
        "INTENT_MODEL_PATH",
        str(repo_root / "ai" / "v1_code" / "using_custom_models" / "model_intent_v2_quantized.pt")
    )
    intent_label_map_path = os.getenv(
        "INTENT_LABEL_MAP_PATH",
        str(repo_root / "ai" / "v1_code" / "using_custom_models" / "label_map.txt")
    )
    tokenizer = im.get_tokenizer()
    model, label_map = im.load_quantized_model(intent_model_path, intent_label_map_path)
    return {
        "intent_model": model,
        "intent_label_map": label_map,
        "tokenizer": tokenizer,
        "intent_mod": im,
        "emotion_mod": emo,
    }

async def _ensure_models_loaded() -> Dict[str, Any]:
    """Async-safe lazy loader. Ensures models are loaded exactly once."""
    if _MODEL_CACHE:
        return _MODEL_CACHE
    async with _MODEL_LOCK:
        if _MODEL_CACHE:
            return _MODEL_CACHE
        loop = asyncio.get_event_loop()
        start = loop.time()
        cache = await loop.run_in_executor(_get_executor(), _load_models_sync)
        _MODEL_CACHE.update(cache)
        dur = loop.time() - start
        try:
            labels_preview = list(cache["intent_label_map"].values()) if isinstance(cache["intent_label_map"], dict) else cache["intent_label_map"]
        except Exception:
            labels_preview = "<unknown labels>"
        logger.info(f"Analysis models loaded in {dur:.3f}s; intent labels={labels_preview}")
        return _MODEL_CACHE

def _map_question_to_competencies(question: str) -> List[str]:
    """질문 키워드 분석을 통해 관련 역량 추출.

    변경: 매칭 실패 시 '모름'으로 간주하여 빈 리스트를 반환합니다.
    (과거엔 전체 역량을 반환해 모든 역량에 점수가 분산되는 문제가 있었음)
    """
    mappings = {
        "Communication": ["설명", "전달", "소통", "발표", "의사소통", "커뮤니케이션", "이야기", "말씀"],
        "Teamwork_Leadership": ["팀", "협업", "리더", "조율", "갈등", "협력", "동료", "함께"],
        "Integrity": ["책임", "윤리", "성실", "신뢰", "약속", "원칙", "정직", "도덕"],
        "Adaptability": ["변화", "적응", "유연", "새로운", "배움", "도전", "학습", "성장"],
        "Job_Competency": ["기술", "역량", "프로젝트", "업무", "개발", "경험", "능력", "지식"],
    }
    
    relevant = []
    for comp, keywords in mappings.items():
        if any(kw in question for kw in keywords):
            relevant.append(comp)
    
    # 매칭 실패 시 비어있는 리스트를 반환하여 해당 문항은 '특정 역량을 묻지 않음'으로 처리
    return relevant if relevant else []

def _compute_scores_sync(cache: Dict[str, Any], qna_history: List[Dict[str, str]]) -> Tuple[Dict[str, int], List[int]]:
    """Synchronous CPU-bound inference logic. Run in thread pool."""
    im = cache["intent_mod"]
    emo = cache["emotion_mod"]
    model = cache["intent_model"]
    tokenizer = cache["tokenizer"]
    label_map = cache["intent_label_map"]

    intent_probs_sum = {label_map[i]: 0.0 for i in range(len(label_map))}
    intent_count = {label_map[i]: 0 for i in range(len(label_map))}  # 역량별 카운트
    emotion_labels: List[int] = []

    for qa in qna_history:
        answer = (qa.get("answer") or "").strip()
        question = (qa.get("question") or "").strip()
        if not answer:
            continue
        
        # 질문 분석으로 관련 역량 식별
        relevant_competencies = _map_question_to_competencies(question)
        logger.debug(f"Question mapped to competencies: {relevant_competencies}")
        
        # Process full answer at once using analyze_paragraph (more efficient)
        # Intent analysis
        try:
            # analyze_paragraph returns list of per-sentence results
            intent_results = im.analyze_paragraph(model, tokenizer, answer, label_map)
            for result in intent_results:
                # Top-2 역량만 반영 (관련 역량 내에서)
                top2 = result.get('top2')
                if top2:
                    for comp_label, prob_val in top2:
                        # 질문과 관련된 역량만 점수 누적
                        if comp_label in relevant_competencies:
                            intent_probs_sum[comp_label] += float(prob_val)
                            intent_count[comp_label] += 1
                    logger.debug("Top-2 intents for sentence: %s", ", ".join(f"{lbl}={val:.2f}" for lbl, val in top2))
                else:
                    # Fallback: top2 없으면 전체 확률 중 임계값 이상만 반영
                    probs = result['probabilities']
                    for idx, prob_val in enumerate(probs):
                        comp_label = label_map[idx]
                        if comp_label in relevant_competencies and prob_val >= 0.3:
                            intent_probs_sum[comp_label] += float(prob_val)
                            intent_count[comp_label] += 1
        except Exception as e:
            logger.warning(f"Intent prediction failed for answer: {e}")
        
        # Emotion analysis - pass full answer as list of sentences
        try:
            sentences = emo.split_sentences(answer)
            sent_results, _ = emo.predict_sentences(sentences)
            for sent_result in sent_results:
                emo_label_str = sent_result.get("pred_label", "uncertain")
                emo_int = {"uncertain": 0, "positive": 1, "negative": 2}.get(emo_label_str, 0)
                emotion_labels.append(emo_int)
        except Exception as e:
            logger.warning(f"Emotion prediction failed for answer: {e}")

    scores: Dict[str, int] = {}
    # 환경변수로 '질문되지 않은' 역량의 기본 점수를 설정 (정수 0~5)
    # 사용자 요구사항: 미출제 역량은 2.5점. API가 int를 요구하므로 기본값 3으로 근사치 적용.
    default_unasked_score = int(os.getenv("NCS_UNASKED_SCORE_INT", "3"))
    for comp, total_prob in intent_probs_sum.items():
        if intent_count[comp] > 0:
            avg_prob = total_prob / intent_count[comp]
            scores[comp] = int(round(avg_prob * 100)) // 20
        else:
            # 해당 역량 관련 질문/답변이 없으면 기본 점수(미출제)를 부여
            scores[comp] = max(0, min(5, default_unasked_score))
    
    total_sentences = sum(intent_count.values())
    logger.info(f"Analysis: computed intent scores from {total_sentences} sentences; competency distribution: {intent_count}; {len(emotion_labels)} emotion labels")
    return scores, emotion_labels

async def compute_intent_scores_and_emotion_labels(qna_history: List[Dict[str, str]]) -> tuple[Dict[str, int], List[int]]:
    """Async entrypoint for computing intent scores and emotion labels.

    Returns:
      (scores, labels)
        scores: dict competency -> 0-100 int
        labels: list of emotion ints (0=uncertain, 1=positive, 2=negative)
    """
    try:
        cache = await _ensure_models_loaded()
        loop = asyncio.get_event_loop()
        return await loop.run_in_executor(_get_executor(), _compute_scores_sync, cache, qna_history)
    except Exception as e:
        logger.exception(f"Failed to compute intent/emotion analysis: {e}")
        default_scores = {
            "Communication": 0,
            "Teamwork_Leadership": 0,
            "Integrity": 0,
            "Adaptability": 0,
            "Job_Competency": 0,
        }
        return default_scores, []


def generate_final_report(
    scores: Dict[str, int],
    emotion_labels: List[int],
    qna_history: List[Dict[str, str]],
    jd_text: str
) -> str:
    """
    Generate a textual report combining scores, emotion labels, and LLM analysis.
    
    Returns a string summary suitable for the 'report' field.
    """
    try:
        # Build prompt for LLM (scores are now 0-5 scale)
        scores_text = "\n".join(f"- {k}: {v}/5" for k, v in scores.items())
        
        emotion_summary = f"{len(emotion_labels)} sentences analyzed"
        if emotion_labels:
            emo_counts = {"uncertain": 0, "positive": 0, "negative": 0}
            for emo_int in emotion_labels:
                emo_label = {0: "uncertain", 1: "positive", 2: "negative"}.get(emo_int, "uncertain")
                emo_counts[emo_label] += 1
            emotion_summary += f" (positive={emo_counts['positive']}, neutral={emo_counts['uncertain']}, negative={emo_counts['negative']})"
        
        qna_text = "\n".join(
            f"Q: {qa.get('question', '')}\nA: {qa.get('answer', '')}"
            for qa in qna_history
        )
        
        # Compute thresholds and indicators for LLM context (adjusted for 0-5 scale)
        avg_score = sum(scores.values()) / len(scores) if scores else 0
        high_scores = [k for k, v in scores.items() if v >= 4]  # 4-5: high performance
        low_scores = [k for k, v in scores.items() if v <= 2]   # 0-2: needs improvement
        
        emo_counts = {"uncertain": 0, "positive": 0, "negative": 0}
        if emotion_labels:
            for emo_int in emotion_labels:
                emo_label = {0: "uncertain", 1: "positive", 2: "negative"}.get(emo_int, "uncertain")
                emo_counts[emo_label] += 1
        
        total_sents = sum(emo_counts.values())
        positive_ratio = emo_counts["positive"] / total_sents if total_sents > 0 else 0
        negative_ratio = emo_counts["negative"] / total_sents if total_sents > 0 else 0
        
        # Build guidance for LLM based on thresholds (0-5 scale)
        performance_note = ""
        if avg_score >= 4.0:
            performance_note = "전반적으로 높은 역량 점수를 보임 (평균 4점 이상). 강점을 중심으로 긍정적 평가."
        elif avg_score >= 3.0:
            performance_note = "전반적으로 중간 수준의 역량 점수 (평균 3-4점). 균형잡힌 평가와 개선 방향 제시."
        else:
            performance_note = "전반적으로 낮은 역량 점수 (평균 3점 미만). 건설적인 피드백과 구체적 개선 방안 제시."
        
        emotion_note = ""
        if positive_ratio >= 0.6:
            emotion_note = "긍정적 감정 비율이 높음 (60% 이상). 자신감과 열정을 반영."
        elif negative_ratio >= 0.3:
            emotion_note = "부정적 감정 비율이 다소 높음 (30% 이상). 긴장도나 불안감 고려."
        else:
            emotion_note = "감정적으로 중립적이고 안정적인 답변."
        
        # Separate system and user prompts for clarity
        system_prompt = (
            "당신은 NCS(국가직무능력표준) 기반 면접 코치입니다. "
            "이 보고서는 면접자(지원자)를 위한 개인 피드백입니다. 2인칭 존댓말로 직접 안내하고, 구체적이고 실행 가능한 제안을 제시하세요. 단수 대상만 가정하며 '여러분/모두' 등 복수 호칭은 금지합니다.\n\n"
            "평가 컨텍스트:\n"
            "- 지원자는 신입/newcomer로 지원한 후보자입니다\n"
            "- 평가 기준은 신입 수준에 맞춰져야 하며, 잠재력과 학습 능력을 중시합니다\n"
            "- 광범위한 실무 경험보다는 기본 역량, 태도, 성장 가능성을 평가합니다\n\n"
            "작성 원칙:\n"
            "- 간결하고 명확하게, 근거(관찰)→판단→다음 행동 순으로 제시\n"
            "- 칭찬과 개선 제안을 균형 있게 포함\n"
            "- 공격적/방어적 표현은 피하고 건설적·격려적 톤 유지"
        )
        
        default_unasked = int(os.getenv("NCS_UNASKED_SCORE_INT", "3"))
        user_prompt = f"""다음 데이터를 바탕으로 면접자(지원자)에게 제공할 개인 피드백 보고서를 작성하세요:

**점수 (0-5 scale):**
{scores_text}
평균 점수: {avg_score:.1f}/5
강점 역량 (4점 이상): {', '.join(high_scores) if high_scores else '없음'}
개선 필요 역량 (2점 이하): {', '.join(low_scores) if low_scores else '없음'}

**점수 해석 가이드 (NCS 기반 리커트 척도):**
미출제(질문되지 않은) 역량은 기본점수 {default_unasked}/5로 처리합니다. (환경변수 NCS_UNASKED_SCORE_INT로 조정 가능)

**Communication (의사소통능력):**
- 5점: 질문 의도 완벽 파악, 구조적·설득적 서술. 행동·결과·의미를 논리로 연결. → 전문적·신뢰도 최고
- 4점: 논리적 전개, 핵심 명확, 예시 적절. 자신의 경험을 통해 이유 설명. → 설득력·자기 인식 뚜렷
- 3점: 문장 구조 명확하나 단조로움. 논리 흐름 있으나 깊이 부족. → 전달력 보통, 무난한 인상
- 2점: 말은 이어지지만 핵심 약함. 피상적 이해, 예시 부재. → 성의는 있으나 흐릿한 인상
- 1점: 논리·맥락 단절, 핵심 전달 실패. 질문 이해 부족, 감정적 반응. → 준비 안 된 인상, 설득력 전무

**Teamwork_Leadership (팀워크·리더십):**
- 5점: 갈등 해결, 리더십, 조율, 격려 모두 포함. 협업의 본질을 스스로 해석함. → 리더형·조율형 인상
- 4점: 팀 내 역할과 기여 구체 설명. 협업 가치 이해, 조율 시도 명확. → 신뢰·존중 기반 협력형 인상
- 3점: 역할 설명 있으나 조율 언급 약함. 협업 과정 이해는 있으나 수동적. → 기본 역량 보임
- 2점: 협업 언급은 있으나 구체 사례 없음. 참여보다 관찰 중심. → 기본 협업 태도만 보임
- 1점: '나만 했다' 식 독단적 언급. 협업 가치를 이해 못함. → 팀 내 갈등 유발 우려

**Integrity (성실·윤리·책임감):**
- 5점: 원칙과 책임을 지키며 성과 창출. 윤리·사명감이 행동 동기. → 강한 신뢰·도덕성
- 4점: 어려움 속 책임감 강조. 원칙 지키며 결과 추구. → 진정성 있는 태도
- 3점: 책임감 표현은 있으나 추상적. 상황 인식만, 자기 성찰 부족. → 신뢰는 주지만 인상 약함
- 2점: 책임감 언급은 하나 구체성 부족. 결과 중심, 진정성 약함. → 성실 의도만 보임
- 1점: 책임 회피, 불성실 표현. 결과보다 변명 중심 사고. → 신뢰 불가

**Adaptability (적응력·유연성):**
- 5점: 불확실한 변화 속에서 주도적으로 해결. 도전 기반의 자기 성장 논리. → 혁신적·유연한 태도
- 4점: 새로운 기술·환경에 적극 대응. 성장 중심 사고. → 적극적 학습 태도
- 3점: 변화 언급은 있으나 구체 사례 부족. 적응은 하나 주도성 미약. → 수용력 보통
- 2점: 변화 수용하나 수동적 태도. 주어진 환경에 순응형. → 소극적 긍정
- 1점: 변화에 부정적, 회피적 발언. 불확실성에 대한 두려움. → 학습 의지 부재

**Job_Competency (직무역량):**
- 5점: 직무 기술과 기업 비전을 자연스럽게 연결. 고객 가치 중심의 사고 반영. → 즉시 투입 가능한 전문가형
- 4점: 실무 중심 기술 활용 명확. 지식→적용→성과의 논리 흐름. → 높은 직무 적합도
- 3점: 관련 경험 있음, 깊이 부족. 실무보단 이론 중심. → 기본 직무 이해 확인
- 2점: 기술·지식 나열, 응용력 부족. 표면적 이해. → 기초 수준 인상
- 1점: 직무 관련성 없음. 단순 흥미 수준. → 업무 이해도 결여

**0점: 모델 신뢰 불가/무응답 등 특수 상황**

**감정 분석:**
{emotion_summary}
긍정 비율: {positive_ratio:.1%}, 부정 비율: {negative_ratio:.1%}

**평가 가이드:**
- {performance_note}
- {emotion_note}

**직무 설명 (JD):**
{jd_text[:2000]}

**면접 Q&A:**
{qna_text[:4000]}

작성 지침(면접자용):
- 2인칭 존댓말로 직접 안내하고, 한 문장 안에 과도한 정보 나열을 피함
- 관찰된 근거 → 당신의 강점/개선 포인트 → 다음 행동(학습/연습 제안) 순으로 간결히 기술
- 450-500자 분량을 유지, 복수 호칭 금지('여러분/모두' 등)

다음 항목을 포함하세요:
1. **종합 평가**: 역량 점수와 감정 분석을 종합한 전반적 소감과 요약(2인칭)
2. **주요 강점**: 높은 점수(4점 이상) 영역과 그에 대한 구체적 근거
3. **개선 영역/다음 행동**: 낮은 점수(2점 이하) 영역에 대한 구체적 보완 포인트와 바로 실천할 수 있는 제안
"""
        
        # Try using LLM with proper message structure
        try:
            from . import question_model as qm
            llm = qm._get_openai_chat()
            if llm is None:
                raise RuntimeError("Chat model unavailable")
            
            # Use chat message format
            from langchain.schema import SystemMessage, HumanMessage
            messages = [
                SystemMessage(content=system_prompt),
                HumanMessage(content=user_prompt)
            ]
            
            response = llm(messages)
            report = response.content if hasattr(response, 'content') else str(response)
            logger.info("Generated final report using LLM")
            return report
        except Exception as e:
            logger.warning(f"LLM report generation failed, using fallback: {e}")
            # Fallback: basic text report
            fallback_report = f"""
면접 평가 보고서

**역량 점수:**
{scores_text}

**감정 분석:**
{emotion_summary}

**종합 평가:**
지원자는 전반적으로 안정적인 답변을 제공했습니다. 
각 역량 점수를 참고하여 추가 검토가 필요합니다.

**추천사항:**
다음 단계로 진행 가능합니다.
"""
            return fallback_report
            
    except Exception as e:
        logger.exception(f"Failed to generate final report: {e}")
        return "Report generation failed. Please review raw data."
