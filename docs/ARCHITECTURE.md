# Kiến trúc hệ thống

## Tổng quan

Hệ thống gồm 4 runtime, .NET là bộ điều phối trung tâm:

```
Client ──REST + SignalR──> .NET 10 API (Clean Architecture)
                            ├─ EF Core 10 ───────────────> SQL Server 2022   (nguồn quan hệ duy nhất)
                            ├─ Qdrant.Client ────────────> Qdrant            (vector; .NET sở hữu mọi thao tác)
                            ├─ Microsoft.Extensions.AI ──> Ollama            (sinh câu trả lời + chấm điểm)
                            └─ HttpClient (X-Internal-Key) > Python ML (FastAPI)  (parse/chunk/embed/rag-eval)
```

- **.NET API** (`Chatbot.Domain / Application / Infrastructure / Api`): auth/RBAC, quản lý tài liệu, chat, điều phối Hangfire, sở hữu toàn bộ thao tác Qdrant và ghi SQL.
- **Python ML** (`ml/`, stateless): parse PDF/DOCX/PPTX, chunk, embed (sentence-transformers), rag-eval (LLM-judge). Không chạm SQL, không ghi Qdrant.
- **SQL Server 2022**: toàn bộ metadata/quan hệ (auth, catalog, documents, rag-linkage, chat, rbl, audit). 30 bảng, 4 schema (`auth/dbo/rag/rbl`).
- **Qdrant**: vector embeddings; 1 collection cho mỗi cặp (embedding model × chunking strategy): `emb_{slug}__strat_{id}`.
- **Ollama**: LLM local (qwen2.5:7b-instruct) cho sinh câu trả lời RAG và làm giám khảo chấm RAGAS.

## Các luồng chính

**Ingestion** (Hangfire `IngestDocumentJob`): upload → validate (magic-byte) + SHA-256 + lưu static file → parse (Python) → chunk (Python) → embed (Python) → upsert Qdrant (.NET, point id UUIDv5 tất định) → ghi `ChunkEmbedding` linkage → `Document.status=indexed`. Idempotent, re-index sạch.

**Chat RAG** (SignalR `ChatHub` / REST fallback): re-auth theo từng tin nhắn → embed câu hỏi (Python) → Qdrant search lọc theo subject → lấy chunk text từ SQL → prompt grounded tiếng Việt → Ollama stream → lưu message + citations. Giới hạn phạm vi: nếu không đủ điểm liên quan → "Tôi không tìm thấy thông tin này trong tài liệu."

**RBL benchmark** (Hangfire `RunExperimentJob`): experiment → fan-out runs (cross-product embedding × chunking × llm) → mỗi câu test: RAG sinh câu trả lời + ngữ cảnh → `EvaluationResult` (idempotent theo câu) → `/rag-eval` chấm 5 metric → tổng hợp `ExperimentRunMetric` (SQL AVG) → dashboard.

## Quyết định kỹ thuật

- **Clean Architecture + MediatR**: use-case dùng chung cho controller, SignalR hub và Hangfire job.
- **RBAC permission-policy**: JWT mang roles; quyền resolve role→permission (cache TTL ngắn = revocation SLA); `[HasPermission("...")]` ở mọi nơi (không dùng `[Authorize(Roles=...)]`).
- **JWT + refresh rotation**: phát hiện reuse → revoke cả family; `SecurityStamp` revalidation cho thu hồi nhanh.
- **Vectors ngoài (Qdrant)**: SQL chỉ giữ linkage `(collection, point_id)`; .NET sở hữu mọi thao tác → không có thành phần thứ 2 ghi Qdrant.
- **Một cascade path/leaf**: tránh lỗi multiple-cascade-path của SQL Server (đã verify bằng integration test Testcontainers).
- **RAGAS**: mặc định LLM-judge local (1 lời gọi/câu, robust); thư viện RAGAS chính thức bật qua `RAGAS_USE_LIBRARY=1`.

## Bảo mật

- Account do admin cấp, buộc đổi mật khẩu lần đầu (gate ở cả REST lẫn hub).
- Mật khẩu băm PBKDF2; refresh token lưu SHA-256.
- Tài liệu lưu ngoài web root; tải về qua endpoint kiểm tra quyền (`PhysicalFileResult`), chống path-traversal trong storage service.
- Rate limit đăng nhập; CORS allow-list; security headers; `X-Internal-Key` cho .NET↔Python; Hangfire dashboard/Scalar chỉ ở Development.
