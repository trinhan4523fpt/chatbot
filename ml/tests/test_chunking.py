from fastapi.testclient import TestClient
from app.main import app

client = TestClient(app)

def test_chunking_fixed_strategy():
    payload = {
        "pages": [{"page": 1, "text": "This is line one. This is line two. This is line three."}],
        "chunk_size": 10,
        "chunk_overlap": 2,
        "strategy": "fixed-512-50"
    }
    response = client.post("/chunk", json=payload)
    assert response.status_code == 200
    data = response.json()
    assert "chunks" in data
    assert len(data["chunks"]) > 0
    # The default/fixed strategy should be used and chunking should happen
    assert all(c["page"] == 1 for c in data["chunks"])

def test_chunking_paragraph_strategy():
    payload = {
        "pages": [{"page": 1, "text": "Paragraph 1.\n\nParagraph 2.\n\nParagraph 3."}],
        "chunk_size": 100,
        "chunk_overlap": 0,
        "strategy": "semantic-paragraph"
    }
    response = client.post("/chunk", json=payload)
    assert response.status_code == 200
    data = response.json()
    assert "chunks" in data
    # paragraph strategy splits by \n\n, so we expect exactly 3 chunks if working correctly
    chunks = data["chunks"]
    assert len(chunks) == 3
    assert "Paragraph 1" in chunks[0]["content"]
    assert "Paragraph 2" in chunks[1]["content"]
    assert "Paragraph 3" in chunks[2]["content"]

def test_chunking_sliding_strategy():
    payload = {
        "pages": [{"page": 1, "text": "ABCDEFGHIJ"}],
        "chunk_size": 2,  # 2 tokens = 8 chars
        "chunk_overlap": 1,  # 1 token = 4 chars
        "strategy": "sliding-window"
    }
    response = client.post("/chunk", json=payload)
    assert response.status_code == 200
    data = response.json()
    assert "chunks" in data
    chunks = data["chunks"]
    # With sliding: win = 8 chars, step = 4 chars.
    # ABCDEFGHIJ length 10.
    # Chunk 0: pos=0, len=8 -> "ABCDEFGH"
    # Chunk 1: pos=4, len=8 -> "EFGHIJ"
    assert len(chunks) == 2
    assert chunks[0]["content"] == "ABCDEFGH"
    assert chunks[1]["content"] == "EFGHIJ"

def test_chunking_sentence_strategy():
    payload = {
        "pages": [{"page": 1, "text": "This is sentence one. This is sentence two! And sentence three?"}],
        "chunk_size": 10,
        "chunk_overlap": 0,
        "strategy": "sentence-based"
    }
    response = client.post("/chunk", json=payload)
    assert response.status_code == 200
    data = response.json()
    assert "chunks" in data
    chunks = data["chunks"]
    # We expect exactly 3 chunks, one for each sentence
    assert len(chunks) == 3
    assert chunks[0]["content"] == "This is sentence one."
    assert chunks[1]["content"] == "This is sentence two!"
    assert chunks[2]["content"] == "And sentence three?"
