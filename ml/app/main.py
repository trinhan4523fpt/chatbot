"""Chatbot ML FastAPI application.

M0 scaffold: exposes /health (no auth, for container healthchecks) and /models
(auth-gated, dimension source of truth). Parse/chunk/embed/rag-eval/finetune endpoints
are added in M3+ with lazy model loading so this skeleton boots without heavy ML deps.
"""
from __future__ import annotations

from fastapi import Depends, FastAPI, File, HTTPException, UploadFile, status

from . import __version__, chunking, embedding, evaluation, parsing
from .models_registry import REGISTRY
from .schemas import (
    ChunkRequest,
    ChunkResponse,
    EmbedRequest,
    EmbedResponse,
    ParsedPage,
    ParseResponse,
    RagEvalRequest,
    RagEvalResponse,
    BenchmarkRequest,
    BenchmarkResponse,
)
from .security import require_internal_key

app = FastAPI(title="Chatbot ML Service", version=__version__)


@app.get("/health")
async def health() -> dict:
    """Liveness probe. Intentionally unauthenticated so Docker healthchecks work."""
    return {"status": "ok", "service": "chatbot-ml", "version": __version__, "models_loaded": []}


@app.get("/models", dependencies=[Depends(require_internal_key)])
async def models() -> dict:
    """Return the embedding-model registry (dimension source of truth)."""
    return {"models": [m.model_dump() for m in REGISTRY]}


@app.post("/parse", dependencies=[Depends(require_internal_key)], response_model=ParseResponse)
async def parse_endpoint(file: UploadFile = File(...)) -> ParseResponse:
    data = await file.read()
    try:
        pages = parsing.parse(file.filename or "", data)
    except ValueError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    return ParseResponse(
        pages=[ParsedPage(page=p, text=t) for p, t in pages],
        page_count=len(pages),
    )


@app.post("/chunk", dependencies=[Depends(require_internal_key)], response_model=ChunkResponse)
async def chunk_endpoint(request: ChunkRequest) -> ChunkResponse:
    chunks = chunking.chunk_pages(
        request.pages,
        request.chunk_size,
        request.chunk_overlap,
        strategy=request.strategy,
    )
    return ChunkResponse(chunks=chunks)


@app.post("/embed", dependencies=[Depends(require_internal_key)], response_model=EmbedResponse)
async def embed_endpoint(request: EmbedRequest) -> EmbedResponse:
    try:
        vectors, dim = embedding.embed(request.texts, request.model, request.input_type)
    except ValueError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=status.HTTP_503_SERVICE_UNAVAILABLE, detail=str(exc)) from exc
    return EmbedResponse(model=request.model, dim=dim, vectors=vectors)


@app.post("/rag-eval", dependencies=[Depends(require_internal_key)], response_model=RagEvalResponse)
async def rag_eval_endpoint(request: RagEvalRequest) -> RagEvalResponse:
    per_item = evaluation.evaluate_items(request.items, request.judge_model)
    return RagEvalResponse(per_item=per_item)


@app.post("/benchmark", dependencies=[Depends(require_internal_key)], response_model=BenchmarkResponse)
async def benchmark_endpoint(request: BenchmarkRequest) -> BenchmarkResponse:
    # strategies are passed as strings like "fixed", "sentence", "sliding", "semantic:multilingual-e5-base"
    results = chunking.benchmark_strategies(request.pages, request.strategies, request.chunk_size, request.chunk_overlap)
    return BenchmarkResponse(results=[r for r in results])
