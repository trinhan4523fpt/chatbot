# Hướng dẫn tích hợp Frontend

Base URL (dev): `http://localhost:5024` · Tất cả route có tiền tố `/api/v1`.

---

## 1. Xác thực

Đăng nhập lấy token, đính kèm vào mọi request sau đó.

```http
POST /api/v1/auth/login
{ "email": "...", "password": "..." }
```
```jsonc
// Trả về:
{ "accessToken": "eyJ...", "accessTokenExpiresAtUtc": "...", "refreshToken": "..." }
```

Mọi request cần header:
```
Authorization: Bearer <accessToken>
```

Access token hết hạn (15 phút) → gọi refresh, KHÔNG bắt user login lại:
```http
POST /api/v1/auth/refresh
{ "refreshToken": "..." }
```

> **Gợi ý:** dùng axios interceptor bắt lỗi `401` → tự refresh → gửi lại request.
> Refresh token xoay vòng: mỗi lần refresh trả token mới, lưu lại đè token cũ.

---

## 2. Chat (streaming qua SignalR) — quan trọng nhất

Chat trả lời **từng chữ (streaming)** qua SignalR, không phải REST thường.

### ⚠️ Bẫy hay gặp: Hub xác thực bằng QUERY, không phải header

```js
import { HubConnectionBuilder } from "@microsoft/signalr";

const conn = new HubConnectionBuilder()
  .withUrl("http://localhost:5024/hubs/chat", {
    accessTokenFactory: () => accessToken   // ← SignalR tự gắn vào ?access_token=
  })
  .withAutomaticReconnect()
  .build();
```

### Lắng nghe 4 sự kiện

```js
let buffer = "";

conn.on("ReceiveToken", (chunk) => {      // từng mẩu chữ → nối vào
  buffer += chunk;
  render(buffer);
});

conn.on("ReceiveReset", () => {           // ⚠️ XOÁ hết, vẽ lại từ đầu
  buffer = "";                            // (xảy ra khi model bị sinh lại)
  render(buffer);
});

conn.on("ReceiveComplete", (result) => {  // xong — kèm câu trả lời đầy đủ + trích dẫn
  render(result.content);                 // dùng cái này làm chuẩn cuối cùng
  showCitations(result.citations);
});

conn.on("Error", (msg) => showError(msg));
```

> **`ReceiveReset` bắt buộc xử lý.** Khi model lỡ trả lời sai (ví dụ dính chữ Hán),
> backend sinh lại và bắn `ReceiveReset` để bảo bạn xoá phần đã hiện. Bỏ qua nó thì
> text sẽ bị nối chồng lộn xộn. Nếu lười, cứ luôn lấy `result.content` ở
> `ReceiveComplete` làm bản cuối cùng thì vẫn đúng.

### Gửi câu hỏi

```js
await conn.start();
await conn.invoke("SendMessage", sessionId, "Câu hỏi của tôi?");
```

### Tạo phiên chat trước khi gửi

```http
POST /api/v1/chat/sessions
{ "subjectId": 2, "title": "Phiên mới" }   →  { "id": 10015 }
```

Các API phiên chat khác:
```
GET  /api/v1/chat/sessions                    danh sách phiên
GET  /api/v1/chat/sessions/{id}/messages      lịch sử tin nhắn
```

> **REST dự phòng** (không stream, dùng khi khó tích hợp SignalR):
> `POST /api/v1/chat/sessions/{id}/messages` với body `{ "content": "..." }` →
> trả về nguyên câu trả lời một lần.

---

## 3. Upload tài liệu

Dùng `multipart/form-data`, không phải JSON:

```js
const fd = new FormData();
fd.append("File", file);          // đúng tên field: "File"
fd.append("SubjectId", subjectId);
fd.append("Title", title);        // optional
// fd.append("ChapterId", chapterId);  // optional

await axios.post("/api/v1/documents", fd, {
  headers: { Authorization: `Bearer ${token}` }  // KHÔNG tự set Content-Type, để browser tự lo
});
```

Trả `201` ngay, nhưng tài liệu **chưa hỏi được liền** — xử lý chạy nền. Theo dõi:

```http
GET /api/v1/documents/{id}/status
→ { "documentStatus": "Indexed", "stage": ..., "state": ... }
```
Khi `documentStatus = "Indexed"` là hỏi được. Định dạng nhận: `.pdf`, `.docx`, `.pptx`.

---

## 4. Màn hình cấu hình (Settings) — dựng UI từ schema

Đừng hardcode form. Gọi 2 API để dựng động:

```http
GET /api/v1/admin/config/schema    ← cấu trúc tab + field (control gì, giới hạn, help)
GET /api/v1/admin/config/options   ← danh sách model/strategy + giá trị đang chọn + ranges
```

`schema` trả về các tab, mỗi field có sẵn cách render:

```jsonc
{
  "tabs": [
    { "key": "retrieval", "title": "Truy hồi", "advanced": false, "fields": [
      { "key": "retrievalTopK", "label": "Số đoạn lấy về (Top K)",
        "type": "number", "min": 1, "max": 50, "help": "...", "requiresReindex": false },
      { "key": "scopeRestriction", "label": "Chỉ trả lời trong tài liệu",
        "type": "bool", "requiresReindex": false }
    ]}
  ]
}
```

| `type` | Render bằng |
|---|---|
| `number` / `decimal` | input số, giới hạn `[min, max]` |
| `bool` | toggle / checkbox |
| `text` | textarea |
| `select` | dropdown, lấy option từ `/options` |

`options` cung cấp danh sách cho các field `select`:

```jsonc
{
  "embeddingModels": [
    { "id": 1, "name": "multilingual-e5-base", "detail": "768 chiều · huggingface",
      "isActive": true, "isSelected": true }   // ← tick sẵn cái đang dùng
  ],
  "chunkingStrategies": [...],
  "llmModels": [...],
  "corpus": { "indexedDocuments": 5, "stale": 0, "needsReindex": false }
}
```

### Lưu cấu hình — chỉ gửi field đã đổi

```http
PUT /api/v1/admin/config
{ "activeEmbeddingModelId": 3, "reindexNow": true }
```

### ⚠️ Bẫy: đổi embedding/chunking làm tài liệu cũ "biến mất"

Field có `requiresReindex: true` (Embedding, Chunking) — đổi thì tài liệu cũ
**chatbot không tìm thấy nữa** cho tới khi index lại. Response `PUT` báo rõ:

```jsonc
{
  "requiresReindex": true,
  "staleDocuments": 5,
  "warning": "5 tài liệu chưa khớp cấu hình mới nên chatbot KHÔNG tìm thấy..."
}
```

**UI nên:** khi user đổi field `requiresReindex`, hiện cảnh báo + hỏi
"Index lại ngay?" → nếu đồng ý thì gửi kèm `"reindexNow": true`. Hoặc để user tự bấm
sau: `POST /api/v1/admin/config/reindex`.

---

## 5. Ghi chú chung

- **CORS:** backend cho phép mọi origin + credentials → gọi từ localhost dev thoải mái.
- **Quyền:** endpoint `/admin/*` cần quyền `admin.config`; chat cần `chat.send_message`.
  Token hết quyền → `403`. UI nên ẩn menu theo role.
- **Lỗi:** `400` = validation (đọc body để biết field nào sai), `401` = token hết hạn
  (refresh), `403` = thiếu quyền, `404` = không tìm thấy, `409` = trùng (vd upload trùng file).
- **Enum trả về dạng snake_case** (vd `"embedding_bench"`), gửi lên cũng vậy.

Chi tiết luồng backend: [PIPELINES.md](../PIPELINES.md) · Sơ đồ DB: [DATABASE.md](DATABASE.md)
