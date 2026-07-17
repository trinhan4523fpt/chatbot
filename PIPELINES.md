# Trace 3 quy trình chính

Tài liệu để đọc code theo luồng: **Upload/Ingest**, **Chat**, **Experiment**.
Mỗi bước ghi `file:dòng` để mở thẳng trong IDE.

> Bổ sung cho [README.md](README.md) (tổng quan) và [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) (kiến trúc).
> Tài liệu này đi sâu vào **thứ tự thực thi**.

---

## Bản đồ nhanh

| Tầng | Thư mục | Vai trò |
|---|---|---|
| API | [src/Chatbot.Api/](src/Chatbot.Api/) | Controller, SignalR Hub, xác thực |
| Application | [src/Chatbot.Application/](src/Chatbot.Application/) | Nghiệp vụ (MediatR), `RagChatService` |
| Infrastructure | [src/Chatbot.Infrastructure/](src/Chatbot.Infrastructure/) | Hangfire job, gọi Python/Ollama/Qdrant, EF Core |
| Domain | [src/Chatbot.Domain/](src/Chatbot.Domain/) | Entity, Enum |
| Python ML | [ml/app/](ml/app/) | Parse, chunk, embed, judge |

**Schema DB:** `dbo` (cấu hình, tài liệu) · `rag` (chunk, embedding) · `auth` · `rbl` (nghiên cứu)
→ [Schemas.cs](src/Chatbot.Infrastructure/Persistence/Schemas.cs)

**Cấu hình đang chạy** (bảng `dbo.SystemConfiguration`, singleton `Id=1`):

| Tham số | Giá trị | Ý nghĩa |
|---|---|---|
| LLM chat | `gemma2:9b` | Sinh câu trả lời |
| LLM judge | `llama3.1:8b` | Chấm benchmark |
| Embedding | `multilingual-e5-base` (768 chiều) | Vector hoá |
| Chunking | `fixed-512-50` | 512 token, overlap 50 |
| `RetrievalTopK` | 5 | Lấy 5 chunk gần nhất |
| `MinRelevanceScore` | 0.30 | Ngưỡng lọc |
| `ScopeRestriction` | true | Bật lọc ngưỡng |
| `HistoryWindowTurns` | 10 | Số lượt hội thoại nhớ lại |

---

# 1. QUY TRÌNH UPLOAD / INGEST

**Tóm tắt:** Upload → validate → lưu đĩa → *(nền)* parse → chunk → embed → Qdrant

### Giai đoạn 1 — HTTP (đồng bộ, trả về ngay)

| # | Bước | Vị trí |
|---|---|---|
| 1 | `POST /api/v1/documents` — nhận file (giới hạn 60MB) | [DocumentsController.cs:32](src/Chatbot.Api/Controllers/DocumentsController.cs#L32) |
| 2 | Handler `UploadDocumentCommandHandler.Handle` | [DocumentFeatures.cs:50](src/Chatbot.Application/Features/Documents/DocumentFeatures.cs#L50) |
| 3 | Kiểm tra quyền trên môn học | [DocumentFeatures.cs:55](src/Chatbot.Application/Features/Documents/DocumentFeatures.cs#L55) |
| 4 | Ghi file tạm, tính SHA-256, lấy **16 byte đầu** | [DiskFileStorageService.cs:23](src/Chatbot.Infrastructure/Storage/DiskFileStorageService.cs#L23) |
| 5 | **Validate loại file** (đuôi + magic bytes) | [FileTypePolicy.cs:18](src/Chatbot.Application/Common/Files/FileTypePolicy.cs#L18) |
| 6 | Chống trùng theo checksum → `ConflictException` | [DocumentFeatures.cs:84](src/Chatbot.Application/Features/Documents/DocumentFeatures.cs#L84) |
| 7 | Tạo row `Document`, `Status = Uploaded` | [DocumentFeatures.cs:107](src/Chatbot.Application/Features/Documents/DocumentFeatures.cs#L107) |
| 8 | Chuyển file tạm → `documents/{subjectId}/{docId}/` | [DiskFileStorageService.cs:53](src/Chatbot.Infrastructure/Storage/DiskFileStorageService.cs#L53) |
| 9 | **Đẩy job vào hàng đợi** `"ingestion"` | [HangfireJobScheduler.cs:8](src/Chatbot.Infrastructure/Jobs/HangfireJobScheduler.cs#L8) |
| 10 | Trả `201 Created` — **file chưa hỏi được ngay** | [DocumentsController.cs:38](src/Chatbot.Api/Controllers/DocumentsController.cs#L38) |

**Định dạng cho phép** — [FileTypePolicy.cs:10-16](src/Chatbot.Application/Common/Files/FileTypePolicy.cs#L10-L16):

| Đuôi | Magic bytes | FileType |
|---|---|---|
| `.pdf` | `%PDF` | `Pdf` |
| `.docx` | PK (zip) | `Docx` |
| `.pptx` | PK (zip) | `Slide` |
| `.ppt` | OLE-CFBF | `Slide` |

> Kiểm tra **cả nội dung**, không chỉ đuôi tên → đổi tên `a.pdf` thành `a.docx` sẽ bị từ chối.
> **`.doc` cũ không được hỗ trợ**, chỉ `.docx`.

### Giai đoạn 2 — Hangfire (chạy nền)

Toàn bộ trong [IngestDocumentJob.cs](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs):

| # | Bước | Dòng |
|---|---|---|
| 11 | `RunAsync` — retry tối đa 3 lần | [:29](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L29) |
| 12 | `Status = Processing`, `Stage = Parse` | [:61](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L61) |
| 13 | **PARSE** → gọi Python `/parse` | [:68](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L68) |
| 14 | **CHUNK** → gọi Python `/chunk` | [:73](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L73) |
| 15 | Xoá chunk cũ, ghi chunk mới vào `rag.DocumentChunk` | [:81-99](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L81-L99) |
| 16 | Tính tên collection Qdrant | [:103](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L103) |
| 17 | **EMBED** theo lô 64 → Python `/embed` | [:110](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L110) |
| 18 | Chặn sai số chiều vector | [:112-116](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L112-L116) |
| 19 | **UPSERT** vào Qdrant | [:139](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L139) |
| 20 | `Status = Indexed` — **giờ mới hỏi được** | [:142](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L142) |

**Xử lý lỗi** — điểm tinh tế:

| Loại lỗi | Xử lý | Dòng |
|---|---|---|
| Lỗi 4xx (file hỏng) | `Status = Failed`, **không retry** | [:154-165](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L154-L165) |
| Lỗi khác (mạng, 5xx) | `Status = Failed`, **ném lại → Hangfire retry** | [:166-176](src/Chatbot.Infrastructure/Jobs/IngestDocumentJob.cs#L166-L176) |

Phân loại tại [AiServiceException.cs:11](src/Chatbot.Application/Common/Exceptions/AiServiceException.cs#L11) — `IsPermanent => 4xx`.

### Phía Python

| Endpoint | Code | Ghi chú |
|---|---|---|
| `POST /parse` | [main.py:42](ml/app/main.py#L42) → [parsing.py:7](ml/app/parsing.py#L7) | pypdf / python-docx / python-pptx |
| `POST /chunk` | [main.py:55](ml/app/main.py#L55) → [chunking.py:17](ml/app/chunking.py#L17) | xem mục *Cách cắt chunk* |
| `POST /embed` | [main.py:66](ml/app/main.py#L66) → [embedding.py:33](ml/app/embedding.py#L33) | `normalize_embeddings=True` |

Tất cả bảo vệ bằng header `X-Internal-Key` → [security.py](ml/app/security.py).

### Qdrant

| Nội dung | Vị trí |
|---|---|
| Tên collection: `{embModel}__strat_{stratId}` | [VectorCollectionNaming.cs:9](src/Chatbot.Application/Common/VectorCollectionNaming.cs#L9) |
| Point ID: **UUIDv5** từ `"{chunkId}:{embModelId}"` | [PointIds.cs:11](src/Chatbot.Infrastructure/Vectors/PointIds.cs#L11) |
| Tạo collection + index `subjectId`/`documentId` | [QdrantVectorStore.cs:9](src/Chatbot.Infrastructure/Vectors/QdrantVectorStore.cs#L9) |
| Upsert | [QdrantVectorStore.cs:26](src/Chatbot.Infrastructure/Vectors/QdrantVectorStore.cs#L26) |

> **Vì sao mỗi (embedding × chunking) một collection?** Vì mỗi embedding model có số chiều
> khác nhau (768 vs 1024) — không thể trộn chung. Đây là lý do **đổi embedding model thì phải
> ingest lại**, còn đổi model chat (gemma2) thì **không cần**.
>
> **Point ID là UUIDv5 tất định** → ingest lại **ghi đè** thay vì tạo bản trùng.

### Trạng thái

`DocumentStatus` — [Enums.cs:14](src/Chatbot.Domain/Enums.cs#L14): `Uploaded → Processing → Indexed` (hoặc `Failed`)

`ProcessingStage` — [Enums.cs:22](src/Chatbot.Domain/Enums.cs#L22): `Parse → Chunk → Embed → Index → Complete`

### Đường vào khác

| Chức năng | Route | Code |
|---|---|---|
| Giảng viên upload | `POST /api/v1/lecturer/documents` | [LecturerDocumentsController.cs:36](src/Chatbot.Api/Controllers/LecturerDocumentsController.cs#L36) — dùng chung `UploadDocumentCommand` |
| Index lại | `POST /api/v1/documents/{id}/reindex` | [DocumentFeatures.cs:311](src/Chatbot.Application/Features/Documents/DocumentFeatures.cs#L311) |
| Xem tiến độ | `GET /api/v1/documents/{id}/status` | [DocumentFeatures.cs:354](src/Chatbot.Application/Features/Documents/DocumentFeatures.cs#L354) |

---

# 2. QUY TRÌNH CHAT

**Tóm tắt:** Câu hỏi → embed → tìm Qdrant → lọc ngưỡng → ghép prompt → gemma2 → **chặn chữ Hán** → lưu

### Đường vào

| Kiểu | Route | Code |
|---|---|---|
| **SignalR** (chính, có streaming) | `/hubs/chat` → `SendMessage` | [ChatHub.cs:22](src/Chatbot.Api/Hubs/ChatHub.cs#L22) |
| **REST** (dự phòng, không stream) | `POST /api/v1/chat/sessions/{id}/messages` | [ChatController.cs:37](src/Chatbot.Api/Controllers/ChatController.cs#L37) |

Hub kiểm tra lại `SecurityStamp` mỗi lần gửi → [ChatHub.cs:24-32](src/Chatbot.Api/Hubs/ChatHub.cs#L24-L32).
Cả 2 đều gọi vào `RagChatService.AnswerAsync`.

### Luồng chính

Toàn bộ trong [RagChatService.cs](src/Chatbot.Application/Features/Chat/RagChatService.cs):

| # | Bước | Dòng |
|---|---|---|
| 1 | `AnswerAsync` | [:38](src/Chatbot.Application/Features/Chat/RagChatService.cs#L38) |
| 2 | Kiểm tra phiên tồn tại + đúng chủ sở hữu | [:42-47](src/Chatbot.Application/Features/Chat/RagChatService.cs#L42-L47) |
| 3 | Đọc cấu hình singleton | [:49](src/Chatbot.Application/Features/Chat/RagChatService.cs#L49) |
| 4 | Chọn model — **phiên ghim đè cấu hình chung** | [:52-57](src/Chatbot.Application/Features/Chat/RagChatService.cs#L52-L57) |
| 5 | Nạp 10 lượt hội thoại gần nhất | [:62-68](src/Chatbot.Application/Features/Chat/RagChatService.cs#L62-L68) |
| 6 | Lưu câu hỏi + tạo chỗ trống (`Streaming`) | [:70-81](src/Chatbot.Application/Features/Chat/RagChatService.cs#L70-L81) |
| 7 | **Embed câu hỏi** (`input_type="query"`) | [:90](src/Chatbot.Application/Features/Chat/RagChatService.cs#L90) |
| 8 | **Tìm Qdrant** — topK=5, lọc theo `subjectId` | [:92](src/Chatbot.Application/Features/Chat/RagChatService.cs#L92) |
| 9 | **Lọc ngưỡng** `score >= 0.30` | [:94-95](src/Chatbot.Application/Features/Chat/RagChatService.cs#L94-L95) |
| 10 | **Không có chunk → trả lời từ chối, KHÔNG gọi LLM** | [:97-102](src/Chatbot.Application/Features/Chat/RagChatService.cs#L97-L102) |
| 11 | Ghép context `[Nguồn i]` + trích dẫn | [:112-126](src/Chatbot.Application/Features/Chat/RagChatService.cs#L112-L126) |
| 12 | Ghép turns: system → lịch sử → prompt | [:128-134](src/Chatbot.Application/Features/Chat/RagChatService.cs#L128-L134) |
| 13 | **Stream từ gemma2** | [:136](src/Chatbot.Application/Features/Chat/RagChatService.cs#L136) |
| 14 | **Vòng lặp chặn chữ Hán** (tối đa 3 lần) | [:143-149](src/Chatbot.Application/Features/Chat/RagChatService.cs#L143-L149) |
| 15 | Cùng đường: **cắt bỏ chữ Hán** | [:151-156](src/Chatbot.Application/Features/Chat/RagChatService.cs#L151-L156) |
| 16 | Lưu câu trả lời + trích dẫn, `Complete` | [:161-176](src/Chatbot.Application/Features/Chat/RagChatService.cs#L161-L176) |

### Cơ chế chặn chữ Hán

Bối cảnh: `qwen2.5` (model cũ) hay trôi sang tiếng Trung. Đã đổi sang `gemma2:9b`
(**0/51 câu dính**, so với qwen2.5 1/12), nhưng vẫn giữ 3 lớp bảo vệ:

| Lớp | Nội dung | Vị trí |
|---|---|---|
| 1 | Prompt cấm chữ Hán (system + nhắc lại cuối prompt) | [:21-31](src/Chatbot.Application/Features/Chat/RagChatService.cs#L21-L31) |
| 2 | Phát hiện → bắt viết lại, tối đa `MaxLanguageRetries = 3` | [:19](src/Chatbot.Application/Features/Chat/RagChatService.cs#L19), [:143](src/Chatbot.Application/Features/Chat/RagChatService.cs#L143) |
| 3 | Vẫn dính → `StripChinese` cắt bỏ | [:153](src/Chatbot.Application/Features/Chat/RagChatService.cs#L153) |

Bộ dò → [AnswerLanguagePolicy.cs](src/Chatbot.Application/Common/AnswerLanguagePolicy.cs):
`ContainsChinese` [:13](src/Chatbot.Application/Common/AnswerLanguagePolicy.cs#L13) ·
`StripChinese` [:31](src/Chatbot.Application/Common/AnswerLanguagePolicy.cs#L31) ·
`IsChinese` [:54](src/Chatbot.Application/Common/AnswerLanguagePolicy.cs#L54)

Phủ 7 dải Unicode (ideograph, dấu câu Trung, fullwidth, compatibility, surrogate pair).
Test → [AnswerLanguagePolicyTests.cs](tests/Chatbot.Application.Tests/AnswerLanguagePolicyTests.cs)

### Prompt

| Thành phần | Nguồn |
|---|---|
| `SystemInstruction` | hằng số [:21-28](src/Chatbot.Application/Features/Chat/RagChatService.cs#L21-L28) |
| Template | **DB** `SystemConfiguration.PromptTemplate` → [Config.cs:53](src/Chatbot.Domain/Entities/Config.cs#L53) |
| Thay `{context}`/`{question}` + nhắc lại | `BuildPrompt` [:208](src/Chatbot.Application/Features/Chat/RagChatService.cs#L208) |

> Template nằm trong **DB**, không phải code. Sửa code seed **không** đổi được DB đang chạy —
> phải dùng migration (xem [20260716081320_VietnameseOnlyPromptTemplate.cs](src/Chatbot.Infrastructure/Persistence/Migrations/20260716081320_VietnameseOnlyPromptTemplate.cs)).

### Gọi Ollama

[OllamaChatCompletionService.cs:20](src/Chatbot.Infrastructure/Ml/OllamaChatCompletionService.cs#L20) — `temperature = 0.2`, stream từng chữ.

### Sự kiện SignalR

| Sự kiện | Khi nào | Dòng |
|---|---|---|
| `ReceiveToken` | mỗi chữ model sinh ra | [ChatHub.cs:41](src/Chatbot.Api/Hubs/ChatHub.cs#L41) |
| `ReceiveReset` | **xoá màn hình** khi sinh lại | [ChatHub.cs:42](src/Chatbot.Api/Hubs/ChatHub.cs#L42) |
| `ReceiveComplete` | xong — kèm kết quả đầy đủ | [ChatHub.cs:44](src/Chatbot.Api/Hubs/ChatHub.cs#L44) |
| `Error` | lỗi | [ChatHub.cs:52](src/Chatbot.Api/Hubs/ChatHub.cs#L52) |

> **`ReceiveReset` cần frontend xử lý**: xoá chữ đã hiện, vẽ lại từ đầu. Nếu bỏ qua, kết quả
> cuối vẫn đúng (`ReceiveComplete` mang câu trả lời sạch) nhưng người dùng thấy text nhấp nháy.

### Trạng thái

`ChatMessageStatus` — [Enums.cs:55](src/Chatbot.Domain/Enums.cs#L55): `Streaming → Complete` · `Cancelled` (ngắt kết nối) · `Error`

---

# 3. QUY TRÌNH EXPERIMENT (BENCHMARK)

**Tóm tắt:** Tạo thí nghiệm → tạo run (tổ hợp) → start → *(nền)* mỗi câu: embed → tìm → sinh → chấm → tổng hợp

### API

[ExperimentsController.cs](src/Chatbot.Api/Controllers/ExperimentsController.cs) — base `api/v1/experiments`:

| Route | Chức năng | Dòng |
|---|---|---|
| `POST /` | Tạo thí nghiệm | [:19](src/Chatbot.Api/Controllers/ExperimentsController.cs#L19) |
| `POST /{id}/runs` | Tạo run (tổ hợp) | [:33](src/Chatbot.Api/Controllers/ExperimentsController.cs#L33) |
| `POST /{id}/start` | **Chạy** → `202 Accepted` | [:42](src/Chatbot.Api/Controllers/ExperimentsController.cs#L42) |
| `GET /{id}/dashboard` | Bảng so sánh | [:50](src/Chatbot.Api/Controllers/ExperimentsController.cs#L50) |
| `GET /runs/{runId}/results` | Chi tiết từng câu | [:55](src/Chatbot.Api/Controllers/ExperimentsController.cs#L55) |

### Tạo run — tích Descartes

[ExperimentFeatures.cs:103-129](src/Chatbot.Application/Features/Experiments/ExperimentFeatures.cs#L103-L129) — 3 vòng lặp lồng nhau:

```
embedding × chunking × llm  →  mỗi tổ hợp = 1 run
2 embedding × 2 chunking × 1 llm = 4 run
```

Trục nào bỏ trống → lấy từ cấu hình active ([:135-136](src/Chatbot.Application/Features/Experiments/ExperimentFeatures.cs#L135-L136)).
Tên run: `"{embedding} | {chunking} | {llm}"` ([:122](src/Chatbot.Application/Features/Experiments/ExperimentFeatures.cs#L122)).

`Start` → đẩy vào hàng đợi `"evaluation"` → [HangfireJobScheduler.cs:11](src/Chatbot.Infrastructure/Jobs/HangfireJobScheduler.cs#L11).

### Job chạy nền

Toàn bộ trong [RunExperimentJob.cs](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs):

| # | Bước | Dòng |
|---|---|---|
| 1 | `RunAsync` — retry 2 lần | [:46](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L46) |
| 2 | `Status = Running`, lưu `CorpusSnapshot` | [:66-76](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L66-L76) |
| 3 | **Tự ingest lại nếu cấu hình khác** | [:80](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L80) → [:294](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L294) |
| 4 | Nạp bộ câu hỏi mẫu của môn | [:82](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L82) |
| 5 | Bỏ qua câu đã `Done` (idempotent) | [:91-94](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L91-L94) |
| 6 | Mỗi câu: embed → tìm Qdrant | [:109-110](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L109-L110) |
| 7 | Ghép context + lưu `EvaluationRetrieval` | [:121-137](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L121-L137) |
| 8 | **Sinh câu trả lời** | [:140](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L140) → [:172](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L172) |
| 9 | **Chấm điểm** | [:147](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L147) → [:201](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L201) |
| 10 | Tính trung bình → `ExperimentRunMetric` | [:148](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L148) → [:235](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L235) |
| 11 | `Status = Done` | [:157-160](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L157-L160) |
| 12 | Run cuối cùng → đóng thí nghiệm | [:270](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L270) |

### Tự ingest lại — `EnsureCorpusIndexedAsync`

Điều kiện kích hoạt ([:314](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L314)):

```csharp
if (chunksCount == 0 || chunksCount != indexedEmbeddingsCount)
```

> Benchmark `bge-m3` mà tài liệu chưa embed bằng model đó → job **tự parse + chunk + embed lại**.
> Vì vậy **run đầu tiên với cấu hình mới luôn lâu hơn**.

### Khác biệt với luồng chat

| | Chat | Experiment |
|---|---|---|
| Lịch sử hội thoại | Có (10 lượt) | **Không** (mỗi câu độc lập, chấm công bằng) |
| Chặn chữ Hán | Có (3 lớp) | **Không** |
| Không có chunk | Trả lời từ chối | Trả lời từ chối ([:175-178](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L175-L178)) |

### Chấm điểm (Judge)

| # | Bước | Vị trí |
|---|---|---|
| 1 | Chọn câu `Done`, sắp theo `Id` | [:203-207](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L203-L207) |
| 2 | Gửi kèm **tên model judge** | [:220](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L220) |
| 3 | HTTP `POST /rag-eval` | [PythonMlClient.cs:56](src/Chatbot.Infrastructure/Ml/PythonMlClient.cs#L56) |
| 4 | Python nhận | [main.py:77](ml/app/main.py#L77) |
| 5 | Gọi Ollama: `format="json"`, `temperature=0` | [evaluation.py:70](ml/app/evaluation.py#L70) |
| 6 | Ghi điểm ngược lại | [:221-230](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L221-L230) |

Prompt chấm điểm → [`_JUDGE_SYSTEM` evaluation.py:30-42](ml/app/evaluation.py#L30-L42).

**Judge chỉ trả JSON toàn số:**

```json
{"faithfulness":0.9,"answer_relevancy":0.8,"context_precision":0.9,
 "context_recall":0.8,"answer_correctness":0.9}
```

> **Đây là lý do lỗi chữ Hán không ảnh hưởng benchmark** — judge không sinh văn xuôi cho người
> dùng đọc. Cũng vì vậy judge dùng model khác chat được (`llama3.1:8b`).

**Đường đi tên model judge:**
[appsettings.json:29](src/Chatbot.Api/appsettings.json#L29) → [IntegrationOptions.cs:28](src/Chatbot.Infrastructure/Options/IntegrationOptions.cs#L28) → [RunExperimentJob.cs:220](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L220) → [PythonMlClient.cs:56](src/Chatbot.Infrastructure/Ml/PythonMlClient.cs#L56) → [evaluation.py:82](ml/app/evaluation.py#L82)

> Biến `RAGAS_JUDGE_MODEL` trong `docker-compose.yml` **chỉ là dự phòng** — .NET luôn gửi tên
> model trong request và Python ưu tiên tham số đó.

### 5 chỉ số

| Chỉ số | Ý nghĩa |
|---|---|
| `faithfulness` | Câu trả lời có bám ngữ cảnh không (không bịa) |
| `answer_relevancy` | Có liên quan câu hỏi không |
| `context_precision` | Chunk lấy về có hữu ích không |
| `context_recall` | Chunk có bao phủ đáp án chuẩn không |
| `answer_correctness` | Có khớp đáp án chuẩn không |

Trung bình tính **bằng SQL**, chỉ trên câu `Done` → [:237-250](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L237-L250).

### Entity

[Rbl.cs](src/Chatbot.Domain/Entities/Rbl.cs) — schema `rbl`:

| Entity | Dòng | Vai trò |
|---|---|---|
| `Experiment` | [:7](src/Chatbot.Domain/Entities/Rbl.cs#L7) | Thí nghiệm |
| `TestQuestion` | [:24](src/Chatbot.Domain/Entities/Rbl.cs#L24) | Câu hỏi + **đáp án chuẩn** |
| `ExperimentRun` | [:41](src/Chatbot.Domain/Entities/Rbl.cs#L41) | 1 tổ hợp cấu hình |
| `EvaluationResult` | [:68](src/Chatbot.Domain/Entities/Rbl.cs#L68) | Kết quả + 5 điểm/câu |
| `EvaluationRetrieval` | [:90](src/Chatbot.Domain/Entities/Rbl.cs#L90) | Chunk đã lấy (truy vết) |
| `ExperimentRunMetric` | [:103](src/Chatbot.Domain/Entities/Rbl.cs#L103) | Trung bình cho dashboard |

### Trạng thái

`RunStatus` — [Enums.cs:84](src/Chatbot.Domain/Enums.cs#L84): `Queued → Running → Done` / `Error` / `Skipped`

`PerQuestionStatus` — [Enums.cs:93](src/Chatbot.Domain/Enums.cs#L93): `Pending → Done` / `Error`

---

# Cách cắt chunk

Code: [ml/app/chunking.py](ml/app/chunking.py) · Khai báo: [DbInitializer.cs:117](src/Chatbot.Infrastructure/Persistence/Seed/DbInitializer.cs#L117)

### 4 bước

| # | Bước | Dòng |
|---|---|---|
| 1 | Đọc **tên** chiến lược → chọn thuật toán | [:25-37](ml/app/chunking.py#L25-L37) |
| 2 | Quy đổi token → ký tự (`× 4`) | [:61-62](ml/app/chunking.py#L61-L62) |
| 3 | Cắt bằng `RecursiveCharacterTextSplitter` | [:60-64](ml/app/chunking.py#L60-L64) |
| 4 | Ghi `token_count` (ước lượng `len ÷ 4`) | [:52](ml/app/chunking.py#L52) |

`_CHARS_PER_TOKEN = 4` → [:14](ml/app/chunking.py#L14)

```
512 token × 4 = 2048 ký tự (tối đa)
 50 token × 4 =  200 ký tự (overlap)
```

### Thứ tự ưu tiên cắt

[:63](ml/app/chunking.py#L63) — `separators=["\n\n", "\n", ". ", " ", ""]`

đoạn văn → xuống dòng → cuối câu → khoảng trắng → (bất đắc dĩ) giữa từ

> **Không cắt cứng ở ký tự 2048** — lùi về ranh giới tự nhiên gần nhất. Vì vậy chunk thực tế
> **thường ngắn hơn nhiều**: đo trên DB thật → median **327 ký tự**, max 691.

### Kiểm chứng con số 4

Đo 40 chunk thật bằng tokenizer thật của `multilingual-e5-base`:

| Chỉ số | Kết quả |
|---|---|
| Tỷ lệ thật | **3.95 ký tự/token** (code giả định 4.0 → lệch 1.25%) |
| Chunk vượt 512 token | **0/40** |
| Token thật | median 82, max 215 |

> Quy ước `4` **chính xác với tiếng Việt**, và không chunk nào vượt giới hạn 512 token
> của model embedding. Tuy nhiên `token_count` lưu trong DB **là ước lượng**, không phải
> token thật — chỉ dùng để thống kê ([chunking.py:3-5](ml/app/chunking.py#L3-L5) có ghi rõ).

### Các chiến lược

| Tên | Thuật toán | Code |
|---|---|---|
| `fixed-512-50` ⭐ **đang dùng** | `RecursiveCharacterTextSplitter` | [:56](ml/app/chunking.py#L56) |
| `fixed-1024-128` | như trên, chunk to gấp đôi | [:56](ml/app/chunking.py#L56) |
| `fixed-size-512-50` | **sliding window** (khác hẳn!) | [:88](ml/app/chunking.py#L88) |
| `recursive-512-50` | `RecursiveCharacterTextSplitter` | [:56](ml/app/chunking.py#L56) |
| `semantic-paragraph` | cắt theo đoạn trống | [:75](ml/app/chunking.py#L75) |
| `sentence-based` | cắt theo câu (underthesea) | [:106](ml/app/chunking.py#L106) |

> ⚠️ **`fixed-512-50` và `fixed-size-512-50` chạy 2 thuật toán KHÁC NHAU** dù tên gần giống.
> [:32](ml/app/chunking.py#L32) bắt chữ `"fixed-size"` và đẩy sang nhánh `sliding`.
> Dễ nhầm khi đọc bảng benchmark.

---

# Điểm cần lưu ý

### Đổi model → cần làm gì?

| Đổi | Ingest lại? | Vì sao |
|---|---|---|
| **Model chat** (gemma2) | ❌ Không | Vector không phụ thuộc model chat |
| **Embedding model** | ✅ **Có** | Số chiều khác (768 vs 1024), collection khác |
| **Chunking strategy** | ✅ **Có** | Cách cắt khác → chunk khác |
| **Model judge** | ❌ Không | Chỉ dùng lúc chấm điểm |

### Seed vs Migration

`DbInitializer` **chỉ chạy khi DB trống**. DB đã có dữ liệu → sửa code seed **không có tác dụng**,
phải viết migration. Đây là lý do có:

- [20260716081320_VietnameseOnlyPromptTemplate.cs](src/Chatbot.Infrastructure/Persistence/Migrations/20260716081320_VietnameseOnlyPromptTemplate.cs) — đổi prompt template
- [20260717025855_SwitchChatModelToGemma2.cs](src/Chatbot.Infrastructure/Persistence/Migrations/20260717025855_SwitchChatModelToGemma2.cs) — đổi model chat

### Điểm yếu đã biết

| Vấn đề | Vị trí | Mức độ |
|---|---|---|
| Ghép điểm judge **theo vị trí** trong danh sách, không theo `index` Python trả về | [RunExperimentJob.cs:221-230](src/Chatbot.Infrastructure/Jobs/RunExperimentJob.cs#L221-L230) | Đúng hiện tại vì Python giữ nguyên thứ tự |
| Nhánh RAGAS truyền model **chat** vào `OllamaEmbeddings` | [evaluation.py:117](ml/app/evaluation.py#L117) | Sẽ lỗi với `llama3.1:8b`, nhưng bị nuốt lỗi ([:59-60](ml/app/evaluation.py#L59-L60)) rồi tự quay về LLM-judge. Chỉ ảnh hưởng khi bật `RAGAS_USE_LIBRARY=1` |
| `token_count` là ước lượng `÷4` | [chunking.py:52](ml/app/chunking.py#L52) | Sai số đo được 1.25% — chấp nhận được |
| CORS cho phép **mọi origin** kèm credentials | [DependencyInjection.cs:115](src/Chatbot.Api/DependencyInjection.cs#L115) | Áp dụng ở **mọi môi trường**. Auth dùng Bearer token (không phải cookie) nên rủi ro thấp, nhưng nên giới hạn trước khi lên production |

### Hạ tầng

| Dịch vụ | Cổng | GPU |
|---|---|---|
| Ollama | 11434 | ✅ **100% GPU** (1.8-6.1s/câu) |
| python-ml | 8000 | ❌ CPU — embedding chỉ 65-86ms, không đáng đổi |
| Qdrant | 6333/6334 | — |
| SQL Server | 1433 | — |

Cấu hình GPU → [docker-compose.yml](docker-compose.yml) mục `ollama.deploy`.
Máy không có GPU NVIDIA → comment khối đó lại.

**Hangfire dashboard:** `/hangfire` — xem job đang chạy/lỗi → [Program.cs:140](src/Chatbot.Api/Program.cs#L140)
