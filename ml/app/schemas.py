"""Request/response models for the ML service."""
from __future__ import annotations

from pydantic import BaseModel


class ParsedPage(BaseModel):
    page: int
    text: str


class ParseResponse(BaseModel):
    pages: list[ParsedPage]
    page_count: int


class ChunkRequest(BaseModel):
    pages: list[ParsedPage]
    strategy: str = "fixed-512-50"
    chunk_size: int = 512
    chunk_overlap: int = 50


class Chunk(BaseModel):
    index: int
    content: str
    page: int | None = None
    token_count: int | None = None


class ChunkResponse(BaseModel):
    chunks: list[Chunk]


class EmbedRequest(BaseModel):
    texts: list[str]
    model: str
    input_type: str = "passage"  # "passage" | "query"


class EmbedResponse(BaseModel):
    model: str
    dim: int
    vectors: list[list[float]]


class RagEvalItem(BaseModel):
    question: str
    answer: str
    contexts: list[str]
    ground_truth: str
    reference_context: str | None = None


class RagEvalRequest(BaseModel):
    items: list[RagEvalItem]
    judge_model: str


class RagEvalScores(BaseModel):
    faithfulness: float | None = None
    answer_relevancy: float | None = None
    context_precision: float | None = None
    context_recall: float | None = None
    answer_correctness: float | None = None


class RagEvalResultItem(BaseModel):
    index: int
    scores: RagEvalScores


class RagEvalResponse(BaseModel):
    per_item: list[RagEvalResultItem]
