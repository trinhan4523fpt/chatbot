using Chatbot.Api.Authorization;
using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Features.Admin.Configuration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Admin;

/// <summary>The active RAG configuration: which embedding model, chunking strategy and LLM chat uses.</summary>
[Authorize]
[ApiController]
[Route("api/v1/admin/config")]
public sealed class SystemConfigurationController(ISender mediator) : ControllerBase
{
    /// <summary>Reads the active configuration, plus whether the corpus still matches it.</summary>
    [HasPermission(Permissions.Admin.Config)]
    [HttpGet]
    public async Task<ActionResult<SystemConfigurationDto>> Get(CancellationToken ct) =>
        Ok(await mediator.Send(new GetSystemConfigurationQuery(), ct));

    /// <summary>
    /// The settings UI structure — tabs and fields — so the frontend can render the config screen
    /// without hardcoding it. Static shape; current values come from the endpoints below.
    /// </summary>
    [HasPermission(Permissions.Admin.Config)]
    [HttpGet("schema")]
    public async Task<ActionResult<ConfigSchemaDto>> Schema(CancellationToken ct) =>
        Ok(await mediator.Send(new GetConfigSchemaQuery(), ct));

    /// <summary>
    /// Everything a settings screen needs in one call: the selectable models and strategies with
    /// the active one flagged, the valid range of each numeric setting, and the corpus status.
    /// </summary>
    [HasPermission(Permissions.Admin.Config)]
    [HttpGet("options")]
    public async Task<ActionResult<ConfigOptionsDto>> Options(CancellationToken ct) =>
        Ok(await mediator.Send(new GetConfigOptionsQuery(), ct));

    /// <summary>
    /// Updates the active configuration. Omitted fields are left unchanged. Changing the embedding
    /// model or chunking strategy makes already-indexed documents unreachable until they are
    /// reindexed — pass reindexNow=true to queue that automatically.
    /// </summary>
    [HasPermission(Permissions.Admin.Config)]
    [HttpPut]
    public async Task<ActionResult<UpdateSystemConfigurationResult>> Update(
        UpdateSystemConfigurationRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(
            new UpdateSystemConfigurationCommand(
                request.ActiveEmbeddingModelId,
                request.ActiveChunkingStrategyId,
                request.ActiveLlmModelId,
                request.RetrievalTopK,
                request.MinRelevanceScore,
                request.ScopeRestriction,
                request.PromptTemplate,
                request.HistoryWindowTurns,
                request.Temperature,
                request.MaxOutputTokens,
                request.ReindexNow),
            ct));

    /// <summary>Reindexes every indexed document against the current configuration.</summary>
    [HasPermission(Permissions.Admin.Config)]
    [HttpPost("reindex")]
    public async Task<ActionResult<object>> Reindex(CancellationToken ct)
    {
        var queued = await mediator.Send(new ReindexCorpusCommand(), ct);
        return Accepted(new { queued });
    }
}
