"""Embedding-model registry — the single source of truth for embedding dimensions.

The .NET API reconciles `dbo.EmbeddingModel.Dimension` against `GET /models` at startup
and fails fast on a mismatch, so dimensions defined here are authoritative.
"""
from __future__ import annotations

from pydantic import BaseModel


class EmbeddingModelInfo(BaseModel):
    key: str
    hf_id: str | None
    provider: str  # "huggingface" | "openai"
    dimension: int
    query_prefix: str = ""
    passage_prefix: str = ""
    is_free: bool = True
    max_input_tokens: int | None = None
    # Vietnamese word segmentation (underthesea) is applied before tokenizing for PhoBERT.
    needs_word_segmentation: bool = False


REGISTRY: list[EmbeddingModelInfo] = [
    EmbeddingModelInfo(
        key="multilingual-e5-base",
        hf_id="intfloat/multilingual-e5-base",
        provider="huggingface",
        dimension=768,
        query_prefix="query: ",
        passage_prefix="passage: ",
        max_input_tokens=512,
    ),
    EmbeddingModelInfo(
        key="phobert-base",
        hf_id="vinai/phobert-base",
        provider="huggingface",
        dimension=768,
        max_input_tokens=256,
        needs_word_segmentation=True,
    ),
    EmbeddingModelInfo(
        key="bge-m3",
        hf_id="BAAI/bge-m3",
        provider="huggingface",
        dimension=1024,
        max_input_tokens=8192,
    ),
    EmbeddingModelInfo(
        key="text-embedding-3-small",
        hf_id=None,
        provider="openai",
        dimension=1536,
        is_free=False,
        max_input_tokens=8191,
    ),
]

REGISTRY_BY_KEY: dict[str, EmbeddingModelInfo] = {m.key: m for m in REGISTRY}
