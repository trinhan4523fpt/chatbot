# Chatbot RAG môn học + Module nghiên cứu RBL

Nền tảng chatbot cho phép sinh viên hỏi đáp **dựa trên tài liệu môn học** (RAG có trích dẫn,
giới hạn phạm vi tài liệu), kèm **module nghiên cứu (RBL)** so sánh RAG vs fine-tuning,
benchmark nhiều chunking strategy / embedding model bằng **RAGAS**.

Backend-first: REST + SignalR + OpenAPI. Stack: **.NET 10**, SQL Server 2022, Qdrant (vector),
Ollama (LLM local), và một **Python ML service** (embeddings + RAGAS + fine-tuning).

> Thiết kế & báo cáo: xem [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) và [docs/RESEARCH-REPORT.md](docs/RESEARCH-REPORT.md).

## Kiến trúc

```
Client ──REST/SignalR──> .NET 10 API (orchestrator)
                          ├─ EF Core 10 ──────────> SQL Server 2022 (metadata, RBAC, chat, runs)
                          ├─ Qdrant.Client ───────> Qdrant (vectors; .NET owns all vector ops)
                          ├─ Microsoft.Extensions.AI > Ollama (sinh câu trả lời, RAGAS judge)
                          └─ HttpClient (X-Internal-Key) > Python ML (parse/chunk/embed/rag-eval)
```
- **.NET** là chủ sở hữu duy nhất của SQL Server và mọi thao tác Qdrant.
- **Python ML** stateless, chỉ tính toán; không chạm SQL, không ghi Qdrant.

## Yêu cầu

- .NET SDK **10.0.300** (xem `global.json`)
- Docker + Docker Compose
- (Tùy chọn) GPU NVIDIA cho Ollama/embeddings

## Bắt đầu (local dev)

> **Lần chạy đầu tải nặng** (cần Docker đang chạy): build image `python-ml` (PyTorch +
> sentence-transformers, vài GB), `ollama-init` kéo model `qwen2.5:7b-instruct` (~4.7GB), và
> lần chat/benchmark đầu tải embedding `multilingual-e5-base` (~1.1GB). Trên máy yếu có thể đổi
> sang model Ollama nhỏ hơn qua `OLLAMA_CHAT_MODEL` trong `.env`.

```powershell
# 1) Hạ tầng: SQL Server, Qdrant, Ollama (+ tự pull model), Python ML
Copy-Item .env.example .env
docker compose up -d            # lần đầu mất vài phút (build image + pull model)

# 2) (tùy chọn) Build & test  — test cần Docker (Testcontainers)
dotnet build Chatbot.slnx
dotnet test  Chatbot.slnx

# 3) Chạy API trên host — TỰ ĐỘNG apply migration + seed (admin, roles, môn học demo, bộ câu test)
dotnet run --project src/Chatbot.Api
```

- API (HTTP): **http://localhost:5024** — Scalar/OpenAPI UI: **http://localhost:5024/scalar/v1**
- Health: `/health` · `/health/ready` · `/health/deps` · Hangfire dashboard (dev): `/hangfire`
- Port đổi trong `src/Chatbot.Api/Properties/launchSettings.json` (http 5024, https 7271).
- Cổng dịch vụ: SQL Server `1433`, Qdrant `6333/6334`, Ollama `11434`, Python ML `8000`.

### Đăng nhập lần đầu (admin seed sẵn ở Development)

- Email `admin@chatbot.local` · mật khẩu `Admin_Dev_P@ssw0rd1` (đặt ở
  `appsettings.Development.json` → `Seed:Admin:Password`). **Bắt buộc đổi mật khẩu lần đầu.**

```powershell
$base = "http://localhost:5024/api/v1"
$login = irm "$base/auth/login" -Method Post -ContentType application/json `
  -Body (@{ email="admin@chatbot.local"; password="Admin_Dev_P@ssw0rd1" } | ConvertTo-Json)
irm "$base/auth/change-password" -Method Post -Headers @{ Authorization = "Bearer $($login.accessToken)" } `
  -ContentType application/json -Body (@{ currentPassword="Admin_Dev_P@ssw0rd1"; newPassword="Admin_New_P@ssw0rd2" } | ConvertTo-Json)
```

> Migration chạy tự động lúc khởi động. Chạy thủ công (tùy chọn):
> `dotnet tool install -g dotnet-ef` rồi
> `dotnet ef database update --project src/Chatbot.Infrastructure --startup-project src/Chatbot.Api`.

Python ML service (khi phát triển riêng, ngoài Docker):

```bash
cd ml
python -m venv .venv && . .venv/Scripts/activate   # PowerShell: .venv\Scripts\Activate.ps1
pip install -r requirements-dev.txt
uvicorn app.main:app --reload
pytest -q
```

## Cấu trúc solution

```
src/
  Chatbot.Domain          # entities, enums, value objects (no deps)
  Chatbot.Application     # use-cases (MediatR), DTOs, AutoMapper, validators, ports
  Chatbot.Infrastructure  # EF Core, Qdrant, Ollama, Python client, Hangfire, storage
  Chatbot.Api             # controllers, SignalR ChatHub, auth/RBAC, OpenAPI/Scalar
tests/                    # Domain / Application / Integration (Testcontainers) / Api
ml/                       # Python FastAPI ML service
```

## Trạng thái (đã triển khai & verify end-to-end)

- ✅ **M0** Init & infra — solution, CPM, docker-compose, ML skeleton, health 3 tầng, CI
- ✅ **M1** Auth & RBAC — account do admin cấp, JWT + refresh rotation, permission-policy, password gate
- ✅ **M2** Catalog + upload tài liệu (static file, magic-byte, SHA-256 dedupe) + download có phân quyền
- ✅ **M3** Ingestion pipeline (Hangfire) + Qdrant + embeddings (Python e5/bge-m3/PhoBERT)
- ✅ **M4** Chat RAG + SignalR streaming + Ollama + citations + giới hạn phạm vi
- ✅ **M5** RBL benchmark + RAGAS (LLM-judge) + dashboard + bộ câu test
- ✅ **M6** Hardening (rate limit, CORS, security headers) + test suite (Testcontainers) + docs

## Tài liệu

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — kiến trúc, luồng, quyết định kỹ thuật
- [docs/RESEARCH-REPORT.md](docs/RESEARCH-REPORT.md) — phương pháp RBL + bảng số liệu RAGAS

## Kiểm thử

```powershell
dotnet test Chatbot.slnx   # unit + integration (Testcontainers SQL Server)
```

## Benchmark RBL & RAGAS

Module nghiên cứu chấm 5 chỉ số kiểu RAGAS: `faithfulness`, `answer_relevancy`,
`context_precision`, `context_recall`, `answer_correctness`.

> **Lưu ý quan trọng về RAGAS:** Mặc định hệ thống dùng **LLM-judge cục bộ** (Ollama, 1 lời gọi/câu)
> để chấm — cho **số thật, ổn định**, và **tránh xung đột phiên bản** giữa thư viện `ragas` và
> `langchain` (lỗi `No module named 'langchain_community.chat_models.vertexai'`). Thư viện
> **RAGAS chính thức** là **tùy chọn**: đặt biến môi trường **`RAGAS_USE_LIBRARY=1`** cho service
> `python-ml` (trong `docker-compose.yml`) sau khi đã pin được phiên bản `ragas`/`langchain` tương
> thích. Việc chấm chạy trên LLM judge **khá chậm trên CPU** — dùng GPU hoặc model nhỏ hơn để tăng tốc.

Chạy benchmark (tóm tắt — chi tiết + bảng số liệu: [docs/RESEARCH-REPORT.md](docs/RESEARCH-REPORT.md)):

1. Upload tài liệu môn học → chờ trạng thái `indexed`.
2. Import bộ câu test: `POST /api/v1/test-questions/import` (hoặc seed `data/seed/test-questions.json`).
3. `POST /api/v1/experiments` → `POST /api/v1/experiments/{id}/runs` → `POST /api/v1/experiments/{id}/start`.
4. Xem kết quả: `GET /api/v1/experiments/{id}/dashboard`.

## Tài khoản & bảo mật

- Tài khoản do Admin cấp (không tự đăng ký).
- Phân quyền theo RBAC (Admin / Lecturer / Student) bằng permission policy.
- File tài liệu lưu static ngoài web root, tải về qua endpoint có kiểm tra quyền.
- Secrets qua `.env` / user-secrets; **không commit** `.env`.
