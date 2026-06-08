using Chatbot.Domain.Enums;

namespace Chatbot.Api.Contracts;

public sealed record CreateExperimentRequest(string Name, ExperimentType Type, long SubjectId, string? Description);

public sealed record CreateRunsRequest(
    IReadOnlyList<long>? EmbeddingModelIds,
    IReadOnlyList<long>? ChunkingStrategyIds,
    IReadOnlyList<long>? LlmModelIds);

public sealed record ImportTestQuestionsRequest(
    long SubjectId, IReadOnlyList<TestQuestionImportDto> Items);

public sealed record TestQuestionImportDto(
    string Question, string GroundTruth, string? ReferenceContext, string? Difficulty, string? ExternalRef);
