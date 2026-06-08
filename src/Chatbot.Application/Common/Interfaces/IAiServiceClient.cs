namespace Chatbot.Application.Common.Interfaces;

public sealed record ParsedPageDto(int Page, string Text);

public sealed record ParseResultDto(IReadOnlyList<ParsedPageDto> Pages, int PageCount);

public sealed record ChunkDto(int Index, string Content, int? Page, int? TokenCount);

public sealed record EmbedResultDto(string Model, int Dim, IReadOnlyList<float[]> Vectors);

public sealed record EmbeddingModelInfoDto(string Key, string? HfId, string Provider, int Dimension, bool IsFree);

public sealed record RagEvalItem(
    string Question, string Answer, IReadOnlyList<string> Contexts, string GroundTruth, string? ReferenceContext);

public sealed record RagEvalScores(
    double? Faithfulness, double? AnswerRelevancy, double? ContextPrecision,
    double? ContextRecall, double? AnswerCorrectness);

public sealed record RagEvalResultItem(int Index, RagEvalScores Scores);

public sealed record RagEvalResult(IReadOnlyList<RagEvalResultItem> PerItem);

/// <summary>Typed client for the stateless Python ML service.</summary>
public interface IAiServiceClient
{
    Task<ParseResultDto> ParseAsync(byte[] content, string fileName, CancellationToken ct = default);

    Task<IReadOnlyList<ChunkDto>> ChunkAsync(
        IReadOnlyList<ParsedPageDto> pages, string strategy, int chunkSize, int chunkOverlap, CancellationToken ct = default);

    Task<EmbedResultDto> EmbedAsync(
        IReadOnlyList<string> texts, string model, string inputType, CancellationToken ct = default);

    Task<IReadOnlyList<EmbeddingModelInfoDto>> GetModelsAsync(CancellationToken ct = default);

    Task<RagEvalResult> RagEvalAsync(
        IReadOnlyList<RagEvalItem> items, string judgeModel, CancellationToken ct = default);
}
