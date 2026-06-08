using Chatbot.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Experiments;

public sealed record ReferenceItemDto(long Id, string Name, string? Detail, bool IsActive);

public sealed record ListEmbeddingModelsQuery : IRequest<IReadOnlyList<ReferenceItemDto>>;

public sealed class ListEmbeddingModelsQueryHandler(IAppDbContext db)
    : IRequestHandler<ListEmbeddingModelsQuery, IReadOnlyList<ReferenceItemDto>>
{
    public async Task<IReadOnlyList<ReferenceItemDto>> Handle(ListEmbeddingModelsQuery request, CancellationToken ct) =>
        await db.EmbeddingModels.AsNoTracking().OrderBy(m => m.Name)
            .Select(m => new ReferenceItemDto(m.Id, m.Name, $"dim {m.Dimension} · {m.Provider}", m.IsActive))
            .ToListAsync(ct);
}

public sealed record ListChunkingStrategiesQuery : IRequest<IReadOnlyList<ReferenceItemDto>>;

public sealed class ListChunkingStrategiesQueryHandler(IAppDbContext db)
    : IRequestHandler<ListChunkingStrategiesQuery, IReadOnlyList<ReferenceItemDto>>
{
    public async Task<IReadOnlyList<ReferenceItemDto>> Handle(ListChunkingStrategiesQuery request, CancellationToken ct) =>
        await db.ChunkingStrategies.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new ReferenceItemDto(s.Id, s.Name, $"size {s.ChunkSize} / overlap {s.ChunkOverlap}", s.IsActive))
            .ToListAsync(ct);
}

public sealed record ListLlmModelsQuery : IRequest<IReadOnlyList<ReferenceItemDto>>;

public sealed class ListLlmModelsQueryHandler(IAppDbContext db)
    : IRequestHandler<ListLlmModelsQuery, IReadOnlyList<ReferenceItemDto>>
{
    public async Task<IReadOnlyList<ReferenceItemDto>> Handle(ListLlmModelsQuery request, CancellationToken ct) =>
        await db.LlmModels.AsNoTracking().OrderBy(m => m.Name)
            .Select(m => new ReferenceItemDto(m.Id, m.Name, m.Type.ToString(), m.IsActive))
            .ToListAsync(ct);
}
