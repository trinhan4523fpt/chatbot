using System.Diagnostics;
using System.Text;
using Chatbot.Application.Common;
using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Chat;

public sealed class RagChatService(
    IAppDbContext db, IAiServiceClient ai, IVectorStore vectors, IChatCompletionService chat)
    : IRagChatService
{
    private const string ScopeMessage = "Tôi không tìm thấy thông tin này trong tài liệu.";

    private const string SystemInstruction =
        "Bạn là trợ lý học tập. Chỉ trả lời dựa trên [NỘI DUNG THAM KHẢO] được cung cấp. " +
        "Nếu thông tin không có trong tài liệu, trả lời đúng câu: \"Tôi không tìm thấy thông tin này trong tài liệu.\" " +
        "Trả lời bằng tiếng Việt, ngắn gọn, và trích dẫn nguồn dạng [Nguồn i].";

    public async Task<ChatAnswerResult> AnswerAsync(
        long sessionId, long userId, IReadOnlyCollection<string> roles, string question,
        Func<string, Task> onToken, CancellationToken ct)
    {
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
                    .Select(c => new { c.Id, c.Content, c.DocumentId, Title = c.Document.Title })
                    .ToListAsync(ct);
                var byId = chunks.ToDictionary(x => x.Id);

                var contextBuilder = new StringBuilder();
                var index = 1;
                foreach (var hit in relevant)
                {
                    if (!byId.TryGetValue(hit.ChunkId, out var chunk))
                    {
                        continue;
                    }

                    contextBuilder.AppendLine($"[Nguồn {index}] {chunk.Content}");
                    var snippet = chunk.Content.Length > 300 ? chunk.Content[..300] : chunk.Content;
                    citations.Add(new ChatCitationDto(
                        chunk.Id, chunk.DocumentId, chunk.Title, (decimal)Math.Round(hit.Score, 6), snippet));
                    index++;
                }

                var turns = new List<ChatTurn> { new("system", SystemInstruction) };
                foreach (var h in history)
                {
                    turns.Add(new ChatTurn(h.Role == ChatRole.User ? "user" : "assistant", h.Content));
                }

                turns.Add(new ChatTurn("user", BuildPrompt(cfg.PromptTemplate, contextBuilder.ToString(), question)));

                await foreach (var delta in chat.StreamAsync(turns, llmModel.Name, ct))
                {
                    content.Append(delta);
                    await onToken(delta);
                }
            }

            stopwatch.Stop();
            assistant.Content = content.ToString();
            assistant.Status = ChatMessageStatus.Complete;
            assistant.LatencyMs = (int)stopwatch.ElapsedMilliseconds;
            foreach (var c in citations)
            {
                db.MessageCitations.Add(new MessageCitation
                {
                    MessageId = assistant.Id, ChunkId = c.ChunkId, DocumentId = c.DocumentId,
                    DocumentTitle = c.DocumentTitle, RelevanceScore = c.Score, Snippet = c.Snippet,
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

    private static string BuildPrompt(string? template, string context, string question)
    {
        template ??= "[NỘI DUNG THAM KHẢO]\n{context}\n\n[CÂU HỎI]\n{question}";
        return template.Replace("{context}", context).Replace("{question}", question);
    }
}
