# Báo cáo nghiên cứu (RBL) — So sánh RAG / chunking / embedding

> Tài liệu khung. Bảng số liệu dưới đây là kết quả **demo đã chạy thật** trên máy CPU với bộ
> test rút gọn (5 câu) để minh hoạ pipeline. Báo cáo chính thức cần chạy trên bộ **50 câu hỏi +
> ground truth** do giảng viên chuẩn bị và tài liệu môn học đầy đủ.

## 1. Mục tiêu

So sánh hiệu quả trả lời hỏi đáp giữa:
- **RAG vs fine-tuning** (`rag_vs_finetune`)
- **Các chiến lược chunking** (`chunking_bench`)
- **Các embedding model**: multilingual-e5-base, text-embedding-3-small (OpenAI), PhoBERT-base, bge-m3 (`embedding_bench`)

## 2. Phương pháp

- **Test set**: bộ câu hỏi + đáp án chuẩn (ground truth) + ngữ cảnh tham chiếu (`reference_context`), nhập qua `POST /api/v1/test-questions/import` hoặc seed `data/seed/test-questions.json`.
- **Pipeline mỗi run**: với từng câu hỏi → embed → truy hồi Qdrant (lọc theo môn học, top-k) → sinh câu trả lời bằng Ollama trên ngữ cảnh truy hồi → lưu câu trả lời + ngữ cảnh.
- **Chấm điểm**: 5 metric kiểu RAGAS, dùng LLM-judge (Ollama, `temperature=0`): faithfulness, answer_relevancy, context_precision, context_recall, answer_correctness. (Bật thư viện RAGAS chính thức: `RAGAS_USE_LIBRARY=1`.)
- **Tổng hợp**: trung bình theo run (SQL `AVG`), hiển thị trên dashboard `GET /api/v1/experiments/{id}/dashboard`.
- **Tái lập**: mỗi run lưu `ConfigSnapshot` + `CorpusSnapshot` (doc ids, collection, model/strategy); chấm idempotent theo từng câu.

## 3. Bảng số liệu RAGAS (demo, 5 câu, CPU)

| Run (embedding · chunking · llm) | Faithfulness | Answer Relevancy | Context Precision | Context Recall | Answer Correctness | Avg latency (ms) |
|---|---|---|---|---|---|---|
| multilingual-e5-base · fixed-512-50 · qwen2.5:7b-instruct | 0.72 | 0.96 | 0.77 | 0.71 | 0.98 | 45 330 |

> Quan sát demo: answer_relevancy/answer_correctness cao (câu trả lời bám đáp án khi có ngữ cảnh);
> context_recall thấp hơn phản ánh một số câu bị giới hạn phạm vi (trả lời "không tìm thấy"). Latency
> cao do chạy mô hình 7B trên CPU — dùng GPU hoặc model nhỏ hơn để tăng tốc.

## 4. Cách chạy benchmark đầy đủ

1. Nạp tài liệu môn học (upload + chờ `indexed`).
2. Import 50 câu test: `POST /api/v1/test-questions/import`.
3. Tạo experiment + runs:
   - `POST /api/v1/experiments` (type = `embedding_bench` / `chunking_bench` / `rag_vs_finetune`)
   - `POST /api/v1/experiments/{id}/runs` với danh sách model/strategy/llm cần so sánh (mỗi cấu hình cần collection Qdrant đã được index trước).
4. `POST /api/v1/experiments/{id}/start` → theo dõi `GET /api/v1/experiments/{id}/dashboard`.

## 5. Ghi chú & hạn chế

- Mỗi cặp (embedding model × chunking strategy) cần được index sẵn vào collection tương ứng trước khi benchmark (re-index tài liệu với cấu hình đó).
- OpenAI embedding chỉ chạy khi có `OPENAI_API_KEY`; nếu không, run được đánh dấu `skipped`/N/A.
- LLM-judge là xấp xỉ thực dụng của RAGAS để tránh xung đột phiên bản; bật RAGAS chính thức cho run "report-grade".
