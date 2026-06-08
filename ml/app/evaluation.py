"""Evaluation of generated answers on five RAGAS-style metrics.

Primary path is a local LLM-as-judge (one Ollama call per item, format=json) which is robust and
avoids the RAGAS<->langchain version conflicts; it scores the same five metrics RAGAS reports.
The official RAGAS library path is opt-in via RAGAS_USE_LIBRARY=1 (kept for report-grade runs).
Any failure degrades to None for that metric/item so the .NET pipeline always completes.
"""
from __future__ import annotations

import json
import logging
import math
import os

import httpx

from .config import get_settings
from .schemas import RagEvalItem, RagEvalResultItem, RagEvalScores

logger = logging.getLogger("chatbot-ml.eval")

_METRIC_KEYS = (
    "faithfulness",
    "answer_relevancy",
    "context_precision",
    "context_recall",
    "answer_correctness",
)

_JUDGE_SYSTEM = (
    "Bạn là giám khảo đánh giá hệ thống hỏi đáp (RAG). "
    "Cho một câu hỏi, câu trả lời sinh ra, các đoạn ngữ cảnh truy hồi, và đáp án chuẩn, "
    "hãy chấm 5 tiêu chí, mỗi tiêu chí một số thực từ 0.0 đến 1.0:\n"
    "- faithfulness: câu trả lời được hỗ trợ bởi ngữ cảnh (không bịa).\n"
    "- answer_relevancy: câu trả lời liên quan tới câu hỏi.\n"
    "- context_precision: ngữ cảnh truy hồi có liên quan/hữu ích cho câu hỏi.\n"
    "- context_recall: ngữ cảnh bao phủ thông tin trong đáp án chuẩn.\n"
    "- answer_correctness: câu trả lời khớp với đáp án chuẩn.\n"
    'Chỉ trả về JSON đúng dạng: '
    '{"faithfulness":0.0,"answer_relevancy":0.0,"context_precision":0.0,'
    '"context_recall":0.0,"answer_correctness":0.0}'
)


def _clean(value) -> float | None:
    try:
        f = float(value)
    except (TypeError, ValueError):
        return None
    if math.isnan(f) or math.isinf(f):
        return None
    return max(0.0, min(1.0, f))


def evaluate_items(items: list[RagEvalItem], judge_model: str) -> list[RagEvalResultItem]:
    if os.getenv("RAGAS_USE_LIBRARY") == "1":
        try:
            return _evaluate_with_ragas(items, judge_model)
        except Exception as exc:  # noqa: BLE001
            logger.warning("RAGAS library failed; falling back to LLM judge: %s", exc)

    settings = get_settings()
    out: list[RagEvalResultItem] = []
    with httpx.Client(timeout=180.0) as client:
        for i, item in enumerate(items):
            out.append(RagEvalResultItem(index=i, scores=_judge_one(client, settings.ollama_url, judge_model, item)))
    return out


def _judge_one(client: httpx.Client, ollama_url: str, judge_model: str, item: RagEvalItem) -> RagEvalScores:
    contexts = "\n".join(f"- {c}" for c in item.contexts) if item.contexts else "(không có)"
    user = (
        f"[CÂU HỎI]\n{item.question}\n\n"
        f"[CÂU TRẢ LỜI SINH RA]\n{item.answer}\n\n"
        f"[NGỮ CẢNH TRUY HỒI]\n{contexts}\n\n"
        f"[ĐÁP ÁN CHUẨN]\n{item.ground_truth}\n"
    )
    try:
        response = client.post(
            f"{ollama_url.rstrip('/')}/api/chat",
            json={
                "model": judge_model,
                "messages": [
                    {"role": "system", "content": _JUDGE_SYSTEM},
                    {"role": "user", "content": user},
                ],
                "stream": False,
                "format": "json",
                "options": {"temperature": 0},
            },
        )
        response.raise_for_status()
        content = response.json()["message"]["content"]
        data = json.loads(content)
        return RagEvalScores(**{k: _clean(data.get(k)) for k in _METRIC_KEYS})
    except Exception as exc:  # noqa: BLE001
        logger.warning("LLM-judge scoring failed for an item: %s", exc)
        return RagEvalScores()


def _evaluate_with_ragas(items: list[RagEvalItem], judge_model: str) -> list[RagEvalResultItem]:
    settings = get_settings()
    from langchain_ollama import ChatOllama, OllamaEmbeddings
    from ragas import evaluate
    from ragas.dataset_schema import EvaluationDataset, SingleTurnSample
    from ragas.embeddings import LangchainEmbeddingsWrapper
    from ragas.llms import LangchainLLMWrapper
    from ragas.metrics import (
        AnswerCorrectness,
        Faithfulness,
        LLMContextPrecisionWithReference,
        LLMContextRecall,
        ResponseRelevancy,
    )

    llm = LangchainLLMWrapper(ChatOllama(model=judge_model, base_url=settings.ollama_url, temperature=0))
    embeddings = LangchainEmbeddingsWrapper(OllamaEmbeddings(model=judge_model, base_url=settings.ollama_url))
    samples = [
        SingleTurnSample(
            user_input=it.question, response=it.answer or "",
            retrieved_contexts=list(it.contexts) if it.contexts else [""],
            reference=it.reference_context or it.ground_truth,
        )
        for it in items
    ]
    result = evaluate(
        dataset=EvaluationDataset(samples=samples),
        metrics=[
            Faithfulness(llm=llm), ResponseRelevancy(llm=llm, embeddings=embeddings),
            LLMContextPrecisionWithReference(llm=llm), LLMContextRecall(llm=llm),
            AnswerCorrectness(llm=llm, embeddings=embeddings),
        ],
        raise_exceptions=False,
    )
    df = result.to_pandas()
    out: list[RagEvalResultItem] = []
    for i in range(len(items)):
        row = df.iloc[i]
        out.append(RagEvalResultItem(index=i, scores=RagEvalScores(
            faithfulness=_clean(row.get("faithfulness")),
            answer_relevancy=_clean(row.get("answer_relevancy") if "answer_relevancy" in row else row.get("response_relevancy")),
            context_precision=_clean(row.get("llm_context_precision_with_reference") if "llm_context_precision_with_reference" in row else row.get("context_precision")),
            context_recall=_clean(row.get("context_recall")),
            answer_correctness=_clean(row.get("answer_correctness")),
        )))
    return out
