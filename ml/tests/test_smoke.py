from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_health_ok():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "ok"


def test_models_returns_full_registry():
    response = client.get("/models")  # no INTERNAL_API_KEY in tests -> allowed
    assert response.status_code == 200
    keys = {m["key"] for m in response.json()["models"]}
    assert {"multilingual-e5-base", "phobert-base", "bge-m3", "text-embedding-3-small"} <= keys


def test_models_dimensions_are_source_of_truth():
    response = client.get("/models")
    dims = {m["key"]: m["dimension"] for m in response.json()["models"]}
    assert dims["multilingual-e5-base"] == 768
    assert dims["phobert-base"] == 768
    assert dims["bge-m3"] == 1024
    assert dims["text-embedding-3-small"] == 1536
