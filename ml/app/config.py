"""Runtime settings, sourced from environment variables."""
from __future__ import annotations

import os
from functools import lru_cache

from pydantic import BaseModel


class Settings(BaseModel):
    internal_api_key: str = os.getenv("INTERNAL_API_KEY", "")
    qdrant_url: str = os.getenv("QDRANT_URL", "http://localhost:6333")
    ollama_url: str = os.getenv("OLLAMA_URL", "http://localhost:11434")
    openai_api_key: str = os.getenv("OPENAI_API_KEY", "")
    hf_home: str = os.getenv("HF_HOME", "/cache/hf")
    default_judge_model: str = os.getenv("RAGAS_JUDGE_MODEL", "qwen2.5:7b-instruct")


@lru_cache
def get_settings() -> Settings:
    return Settings()
