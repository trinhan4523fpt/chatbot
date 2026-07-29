using System.Diagnostics;
using System.Text;
using Chatbot.Application.Common;
using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Application.Features.Payment;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Chat;

public sealed class RagChatService(
    IAppDbContext db, IPaymentDbContext paymentDb,
    IAiServiceClient ai, IVectorStore vectors, IChatCompletionService chat)
    : IRagChatService
{
    private const string ScopeMessage = "I could not find this information in the documents.";

    /// <summary>Regeneration attempts allowed when the model answers with Chinese characters.</summary>
    private const int MaxLanguageRetries = 3;

    private const string SystemInstruction =
        "You are a study assistant for a university. " +
        "LANGUAGE RULE (MANDATORY, no exceptions): the entire answer MUST be written 100% in English. " +
        "Do NOT use Chinese, Chinese characters, Vietnamese, or any other language. " +
        "If the reference material is in another language, translate it into English. " +
        "Answer only from the [REFERENCE CONTENT] provided. " +
        "If the information is not in the documents, reply exactly: \"I could not find this information in the documents.\" " +
        "Be concise and cite sources as [Source i].";

    private const string LanguageReminder =
        "Reminder: answer entirely in English, with no Chinese characters.";

    private const string RetryInstruction =
        "The answer above contained Chinese characters and is INVALID. " +
        "Rewrite the whole answer using only English, with absolutely no Chinese characters. " +
        "Output only the corrected answer, with no extra explanation.";

    public async Task<ChatAnswerResult> AnswerAsync(
        long sessionId, long userId, IReadOnlyCollection<string> roles, string question,
        Func<string, Task> onToken, Func<Task> onReset, CancellationToken ct)
    {
        // ── Kiểm tra & trừ token trước khi gọi AI (1 câu hỏi = 1 token) ──────────────
        // Admin & Lecturer không bị trừ token.
        var isStudentRole = !roles.Contains("Admin") && !roles.Contains("Lecturer");
        if (isStudentRole)
        {
            var tokenResult = await ConsumeToken.ExecuteAsync(
                paymentDb,
                new ConsumeToken.Command(
                    UserId: userId,
                    Amount: 1,
                    Description: $"Câu hỏi trong phiên #{sessionId}"),
                ct);

            if (!tokenResult.Allowed)
                throw new ForbiddenException(tokenResult.Reason
                    ?? "Không đủ token. Vui lòng mua gói để tiếp tục dùng chatbot.");
        }
        // ─────────────────────────────────────────────────────────────────────────────

        var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new NotFoundException("Không tìm thấy phiên chat.");
        if (session.UserId != userId)
        {
            throw new ForbiddenException("Bạn không có quyền gửi tin nhắn trong phiên này.");
        }

        var cfg = await db.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("System configuration is missing.");

        var embeddingModelId = session.PinnedEmbeddingModelId ?? cfg.ActiveEmbeddingModelId
            ?? throw new InvalidOperationException("No active embedding model configured.");
        var strategyId = session.PinnedChunkingStrategyId ?? cfg.ActiveChunkingStrategyId
            ?? throw new InvalidOperationException("No active chunking strategy configured.");
        var llmModelId = session.PinnedLlmModelId ?? cfg.ActiveLlmModelId
            ?? throw new InvalidOperationException("No active LLM configured.");

        var embeddingModel = await db.EmbeddingModels.FirstAsync(m => m.Id == embeddingModelId, ct);
        var llmModel = await db.LlmModels.FirstAsync(m => m.Id == llmModelId, ct);

        var history = await db.ChatMessages.AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.Status == ChatMessageStatus.Complete)
            .OrderByDescending(m => m.Id)
            .Take(cfg.HistoryWindowTurns * 2)
            .OrderBy(m => m.Id)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync(ct);

        db.ChatMessages.Add(new ChatMessage
        {
            SessionId = sessionId, Role = ChatRole.User, Content = question,
            Status = ChatMessageStatus.Complete, EmbeddingModelId = embeddingModelId,
        });
        var assistant = new ChatMessage
        {
            SessionId = sessionId, Role = ChatRole.Assistant, Content = string.Empty,
            Status = ChatMessageStatus.Streaming, LlmModelId = llmModelId, EmbeddingModelId = embeddingModelId,
        };
        db.ChatMessages.Add(assistant);
        await db.SaveChangesAsync(ct);

        var stopwatch = Stopwatch.StartNew();
        var content = new StringBuilder();
        var citations = new List<ChatCitationDto>();
        var scopeRestricted = false;

        try
        {
            var embedding = await ai.EmbedAsync([question], embeddingModel.Name, "query", ct);
            var collection = VectorCollectionNaming.For(embeddingModel.QdrantCollectionName, strategyId);
            var hits = await vectors.SearchAsync(collection, embedding.Vectors[0], cfg.RetrievalTopK, session.SubjectId, ct);

            var minScore = (float)cfg.MinRelevanceScore;
            var relevant = (cfg.ScopeRestriction ? hits.Where(h => h.Score >= minScore) : hits).ToList();

            if (relevant.Count == 0)
            {
                scopeRestricted = true;
                content.Append(ScopeMessage);
                await onToken(ScopeMessage);
            }
            else
            {
                var chunkIds = relevant.Select(h => h.ChunkId).ToList();
                var chunks = await db.DocumentChunks.AsNoTracking()
                    .Where(c => chunkIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Content, c.DocumentId, c.PageNumber, Title = c.Document.Title })
                    .ToListAsync(ct);
                var byId = chunks.ToDictionary(x => x.Id);

                var contextBuilder = new StringBuilder();
                var index = 1;
                foreach (var hit in relevant)
                {
                    if (!byId.TryGetValue(hit.ChunkId, out var chunk))
                    {
                        // Qdrant returned a point whose chunk no longer exists in the DB — an orphan
                        // left behind by a reindex or delete. Skip it rather than cite a missing chunk.
                        continue;
                    }

                    contextBuilder.AppendLine($"[Source {index}] {chunk.Content}");
                    var snippet = chunk.Content.Length > 300 ? chunk.Content[..300] : chunk.Content;
                    citations.Add(new ChatCitationDto(
                        chunk.Id, chunk.DocumentId, chunk.Title, (decimal)Math.Round(hit.Score, 6),
                        snippet, chunk.PageNumber));
                    index++;
                }

                // Every hit was an orphan (its chunk is gone from the DB), so there is no real context
                // to answer from. Refuse instead of sending an empty context and letting the model
                // invent an answer with no sources.
                if (citations.Count == 0)
                {
                    scopeRestricted = true;
                    content.Append(ScopeMessage);
                    await onToken(ScopeMessage);
                }
                else
                {
                    var turns = new List<ChatTurn> { new("system", SystemInstruction) };
                    foreach (var h in history)
                    {
                        turns.Add(new ChatTurn(h.Role == ChatRole.User ? "user" : "assistant", h.Content));
                    }

                    turns.Add(new ChatTurn("user", BuildPrompt(cfg.PromptTemplate, contextBuilder.ToString(), question)));

                    var sampling = new ChatSamplingOptions((float)cfg.Temperature, cfg.MaxOutputTokens);
                    var answer = await StreamAnswerAsync(turns, llmModel.Name, sampling, onToken, ct);

                    // Qwen drifts into Chinese despite the instruction, and a corrective retry can drift
                    // again, so keep regenerating while the answer is invalid. Each attempt replays the
                    // rejected text so the model sees what to avoid; the client clears the bad partial
                    // via onReset. Attempts are bounded, and a last resort strips the stray characters
                    // rather than let any Chinese reach the user.
                    for (var attempt = 0; attempt < MaxLanguageRetries && AnswerLanguagePolicy.ContainsChinese(answer); attempt++)
                    {
                        await onReset();
                        turns.Add(new ChatTurn("assistant", answer));
                        turns.Add(new ChatTurn("user", RetryInstruction));
                        answer = await StreamAnswerAsync(turns, llmModel.Name, sampling, onToken, ct);
                    }

                    if (AnswerLanguagePolicy.ContainsChinese(answer))
                    {
                        answer = AnswerLanguagePolicy.StripChinese(answer);
                        await onReset();
                        await onToken(answer);
                    }

                    content.Append(answer);
                }
            }

            stopwatch.Stop();
            assistant.Content = content.ToString();
            assistant.Status = ChatMessageStatus.Complete;
            assistant.LatencyMs = (int)stopwatch.ElapsedMilliseconds;

            // The chunks cleared the score threshold but the model still judged them irrelevant and
            // refused. Showing citations under a "not found" answer is contradictory, so drop them.
            if (content.ToString().Contains(ScopeMessage, StringComparison.OrdinalIgnoreCase))
            {
                citations.Clear();
                scopeRestricted = true;
            }

            foreach (var c in citations)
            {
                db.MessageCitations.Add(new MessageCitation
                {
                    MessageId = assistant.Id, ChunkId = c.ChunkId, DocumentId = c.DocumentId,
                    DocumentTitle = c.DocumentTitle, RelevanceScore = c.Score, Snippet = c.Snippet,
                    PageNumber = c.PageNumber,
                });
            }

            await db.SaveChangesAsync(ct);
            return new ChatAnswerResult(
                assistant.Id, scopeRestricted, content.ToString(), (int)stopwatch.ElapsedMilliseconds, citations);
        }
        catch (OperationCanceledException)
        {
            assistant.Content = content.ToString();
            assistant.Status = ChatMessageStatus.Cancelled;
            assistant.LatencyMs = (int)stopwatch.ElapsedMilliseconds;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch
        {
            assistant.Content = content.ToString();
            assistant.Status = ChatMessageStatus.Error;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<string> StreamAnswerAsync(
        List<ChatTurn> turns, string model, ChatSamplingOptions sampling, Func<string, Task> onToken,
        CancellationToken ct)
    {
        var buffer = new StringBuilder();
        await foreach (var delta in chat.StreamAsync(turns, model, sampling, ct))
        {
            buffer.Append(delta);
            await onToken(delta);
        }

        return buffer.ToString();
    }

    private static string BuildPrompt(string? template, string context, string question)
    {
        template ??= "[REFERENCE CONTENT]\n{context}\n\n[QUESTION]\n{question}";
        var prompt = template.Replace("{context}", context).Replace("{question}", question);
        return prompt + "\n\n" + LanguageReminder;
    }
}
