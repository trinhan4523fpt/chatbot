"""Chatbot ML service — stateless FastAPI compute service.

Owns: document parsing, chunking, embedding (HuggingFace sentence-transformers),
RAGAS evaluation and (optional) fine-tuning. It never touches SQL Server and never
writes Qdrant — the .NET API owns all relational and vector persistence.
"""

__version__ = "0.1.0"
