"""Chunking: split parsed pages into overlapping chunks, preserving page numbers.

chunk_size/chunk_overlap are expressed in tokens; we approximate ~4 chars/token for the
character splitter and estimate token_count similarly. This keeps the service dependency-light
while remaining adequate for the benchmark's relative comparison of strategies.
"""
from __future__ import annotations

from .schemas import Chunk, ParsedPage
import re
import time

_CHARS_PER_TOKEN = 4


def _split_sentences(text: str) -> list[str]:
    """Split into sentences: underthesea (Vietnamese) first, then a punctuation-based fallback."""
    try:
        from underthesea import sent_tokenize

        sentences = sent_tokenize(text)
    except Exception:
        sentences = re.split(r"(?<=[.!?])\s+", text)
    return [s.strip() for s in sentences if s.strip()]


def chunk_pages(pages: list[ParsedPage], chunk_size: int, chunk_overlap: int, strategy: str = "fixed") -> list[Chunk]:
    """Chunk pages using different strategies.

    Supported strategies:
    - "fixed": uses RecursiveCharacterTextSplitter (character-based with separators).
    - "paragraph": split by blank lines / newlines (no overlap).
    - "sliding": simple fixed-size sliding window over characters with overlap.
    - "sentence": group sentences into chunks by token budget.
    - "semantic[:model_key]": semantic similarity-based chunking.
    """
    strategy_lower = (strategy or "fixed").lower()
    if "semantic-paragraph" in strategy_lower:
        strategy = "paragraph"
    elif "semantic" in strategy_lower:
        strategy = strategy_lower
    elif "paragraph" in strategy_lower:
        strategy = "paragraph"
    elif "sliding" in strategy_lower or "fixed-size" in strategy_lower:
        strategy = "sliding"
    elif "sentence" in strategy_lower:
        strategy = "sentence"
    elif "char" in strategy_lower:
        strategy = "char"
    else:
        strategy = "fixed"

    chunks: list[Chunk] = []
    index = 0

    # helper to append a piece
    def _append(piece: str, page_no: int | None):
        nonlocal index
        piece = piece.strip()
        if not piece:
            return
        chunks.append(Chunk(
            index=index,
            content=piece,
            page=page_no,
            token_count=max(1, len(piece) // _CHARS_PER_TOKEN),
        ))
        index += 1


    if strategy == "fixed":
        # keep existing behavior using langchain splitter
        from langchain_text_splitters import RecursiveCharacterTextSplitter

        splitter = RecursiveCharacterTextSplitter(
            chunk_size=max(1, chunk_size) * _CHARS_PER_TOKEN,
            chunk_overlap=max(0, chunk_overlap) * _CHARS_PER_TOKEN,
            separators=["\n\n", "\n", ". ", " ", ""],
        )

        for page in pages:
            text = page.text.strip()
            if not text:
                continue
            for piece in splitter.split_text(text):
                _append(piece, page.page)

        return chunks

    if strategy == "paragraph":
        for page in pages:
            text = page.text.strip()
            if not text:
                continue
            # split by double-newline, fall back to single newline
            parts = [p for p in re.split(r"\n\s*\n", text) if p.strip()]
            if not parts:
                parts = [p for p in text.splitlines() if p.strip()]
            for piece in parts:
                _append(piece, page.page)
        return chunks

    if strategy == "sliding":
        win = max(1, chunk_size) * _CHARS_PER_TOKEN
        step = max(1, chunk_size - max(0, chunk_overlap)) * _CHARS_PER_TOKEN
        for page in pages:
            text = page.text or ""
            text = text.strip()
            if not text:
                continue
            pos = 0
            while pos < len(text):
                piece = text[pos: pos + win]
                _append(piece, page.page)
                if pos + win >= len(text):
                    break
                pos += step  # step = win - overlap, so consecutive windows overlap
        return chunks

    if strategy == "char":
        # Chia thuần theo số ký tự, chunk_size được hiểu trực tiếp là số ký tự (không quy đổi token).
        char_size = max(1, chunk_size)
        char_step = max(1, chunk_size - max(0, chunk_overlap))
        for page in pages:
            text = (page.text or "").strip()
            if not text:
                continue
            pos = 0
            while pos < len(text):
                piece = text[pos: pos + char_size]
                _append(piece, page.page)
                if pos + char_size >= len(text):
                    break
                pos += char_step
        return chunks

    if strategy == "sentence":
        for page in pages:
            text = page.text or ""
            text = text.strip()
            if not text:
                continue
            sentences = _split_sentences(text)
            if not sentences:
                continue

            # Group sentences into chunks up to the token budget, keeping sentences whole.
            cur: list[str] = []
            cur_tokens = 0
            for s in sentences:
                tokens = max(1, len(s) // _CHARS_PER_TOKEN)
                if cur and cur_tokens + tokens > chunk_size:
                    _append(" ".join(cur), page.page)
                    cur = [s]
                    cur_tokens = tokens
                else:
                    cur.append(s)
                    cur_tokens += tokens
            if cur:
                _append(" ".join(cur), page.page)

        if chunk_overlap > 0:
            return _apply_overlap(chunks, chunk_overlap)
        return chunks

    if strategy.startswith("semantic"):
        # optional model key: semantic[:model_key]
        parts = strategy.split(":", 1)
        model_key = parts[1] if len(parts) > 1 and parts[1] else "multilingual-e5-base"

        from .embedding import embed

        for page in pages:
            text = page.text or ""
            text = text.strip()
            if not text:
                continue

            sentences = _split_sentences(text)
            if not sentences:
                continue

            # embeddings (may raise) - fallback to sentence strategy if fails
            try:
                vectors, _dim = embed(sentences, model_key, "passage")
            except Exception:
                # fallback to sentence-based chunking
                return chunk_pages(pages, chunk_size, chunk_overlap, strategy="sentence")

            # ensure normalization (embedding.embed tends to normalize for HF models)
            def _dot(a, b):
                return sum(x * y for x, y in zip(a, b))

            # compute similarities between adjacent sentences
            sims = []
            for i in range(1, len(vectors)):
                try:
                    sim = _dot(vectors[i - 1], vectors[i])
                except Exception:
                    sim = 0.0
                sims.append(sim)

            # greedy segmentation: start new chunk when token budget exceeded or similarity < threshold
            threshold = 0.70
            cur = []
            cur_tokens = 0
            for i, s in enumerate(sentences):
                tokens = max(1, len(s) // _CHARS_PER_TOKEN)
                boundary = False
                if cur and cur_tokens + tokens > chunk_size:
                    boundary = True
                if i > 0 and sims[i - 1] < threshold and cur:
                    boundary = True

                if boundary:
                    _append(" ".join(cur), page.page)
                    cur = [s]
                    cur_tokens = tokens
                else:
                    cur.append(s)
                    cur_tokens += tokens

            if cur:
                _append(" ".join(cur), page.page)

        # apply overlap
        if chunk_overlap > 0:
            return _apply_overlap(chunks, chunk_overlap)
        return chunks

    # unknown strategy: fallback to fixed
    return chunk_pages(pages, chunk_size, chunk_overlap, strategy="fixed")


def benchmark_strategies(pages: list[ParsedPage], strategies: list[str], chunk_size: int, chunk_overlap: int) -> list[dict]:
    """Run simple benchmarks for given strategies and return metrics per strategy."""
    results: list[dict] = []
    for strat in strategies:
        start = time.perf_counter()
        ch = chunk_pages(pages, chunk_size, chunk_overlap, strategy=strat)
        elapsed = (time.perf_counter() - start) * 1000.0
        total_tokens = sum((c.token_count or 0) for c in ch)
        mean_tokens = (total_tokens / len(ch)) if ch else 0
        results.append({
            "strategy": strat,
            "chunk_count": len(ch),
            "total_tokens": total_tokens,
            "mean_tokens": mean_tokens,
            "time_ms": elapsed,
        })
    return results


def _apply_overlap(chunks: list[Chunk], chunk_overlap_tokens: int) -> list[Chunk]:
    """Return new chunks with prefix overlap applied (approx by chars)."""
    if not chunks or chunk_overlap_tokens <= 0:
        return chunks
    overlap_chars = chunk_overlap_tokens * _CHARS_PER_TOKEN
    new_chunks: list[Chunk] = []
    for i, c in enumerate(chunks):
        if i == 0:
            new_chunks.append(c)
            continue
        prev = chunks[i - 1]
        prev_suffix = prev.content[-overlap_chars:] if len(prev.content) > overlap_chars else prev.content
        content = (prev_suffix + " " + c.content).strip()
        new_chunks.append(Chunk(index=c.index, content=content, page=c.page,
                                token_count=max(1, len(content) // _CHARS_PER_TOKEN)))
    return new_chunks
