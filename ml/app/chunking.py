"""Chunking: split parsed pages into overlapping chunks, preserving page numbers.

chunk_size/chunk_overlap are expressed in tokens; we approximate ~4 chars/token for the
character splitter and estimate token_count similarly. This keeps the service dependency-light
while remaining adequate for the benchmark's relative comparison of strategies.
"""
from __future__ import annotations

from .schemas import Chunk, ParsedPage

_CHARS_PER_TOKEN = 4


def chunk_pages(pages: list[ParsedPage], chunk_size: int, chunk_overlap: int) -> list[Chunk]:
    from langchain_text_splitters import RecursiveCharacterTextSplitter

    splitter = RecursiveCharacterTextSplitter(
        chunk_size=max(1, chunk_size) * _CHARS_PER_TOKEN,
        chunk_overlap=max(0, chunk_overlap) * _CHARS_PER_TOKEN,
        separators=["\n\n", "\n", ". ", " ", ""],
    )

    chunks: list[Chunk] = []
    index = 0
    for page in pages:
        text = page.text.strip()
        if not text:
            continue
        for piece in splitter.split_text(text):
            piece = piece.strip()
            if not piece:
                continue
            chunks.append(Chunk(
                index=index,
                content=piece,
                page=page.page,
                token_count=max(1, len(piece) // _CHARS_PER_TOKEN),
            ))
            index += 1

    return chunks
