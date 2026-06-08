"""Embeddings via local HuggingFace sentence-transformers (and optional OpenAI).

PhoBERT uses underthesea word-segmentation; query and passage share ONE segmentation
code path (test-asserted parity). e5 uses query:/passage: prefixes. Vectors are L2-normalized
so cosine == dot product in Qdrant.
"""
from __future__ import annotations

from functools import lru_cache

from .config import get_settings
from .models_registry import REGISTRY_BY_KEY, EmbeddingModelInfo


@lru_cache(maxsize=4)
def _load_st(hf_id: str):
    from sentence_transformers import SentenceTransformer

    return SentenceTransformer(hf_id)


def _prepare(texts: list[str], info: EmbeddingModelInfo, input_type: str) -> list[str]:
    if info.needs_word_segmentation:
        from underthesea import word_tokenize

        # Single shared segmentation path for both query and passage.
        return [word_tokenize(t, format="text") for t in texts]

    prefix = info.query_prefix if input_type == "query" else info.passage_prefix
    return [prefix + t for t in texts]


def embed(texts: list[str], model_key: str, input_type: str) -> tuple[list[list[float]], int]:
    info = REGISTRY_BY_KEY.get(model_key)
    if info is None:
        raise ValueError(f"Unknown embedding model: '{model_key}'")

    if not texts:
        return [], info.dimension

    if info.provider == "openai":
        return _embed_openai(texts, info)

    prepared = _prepare(texts, info, input_type)
    model = _load_st(info.hf_id)  # type: ignore[arg-type]
    vectors = model.encode(prepared, normalize_embeddings=True, convert_to_numpy=True)
    return [v.tolist() for v in vectors], int(model.get_sentence_embedding_dimension())


def _embed_openai(texts: list[str], info: EmbeddingModelInfo) -> tuple[list[list[float]], int]:
    settings = get_settings()
    if not settings.openai_api_key:
        raise RuntimeError("OPENAI_API_KEY not configured; OpenAI embedding unavailable.")

    from openai import OpenAI

    client = OpenAI(api_key=settings.openai_api_key)
    response = client.embeddings.create(model="text-embedding-3-small", input=texts)
    return [d.embedding for d in response.data], info.dimension
