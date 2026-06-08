using Chatbot.Api.Authorization;
using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Features.Experiments;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/v1/experiments")]
public sealed class ExperimentsController(ISender mediator) : ControllerBase
{
    [HasPermission(Permissions.Admin.DashboardView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExperimentDto>>> List(CancellationToken ct) =>
        Ok(await mediator.Send(new ListExperimentsQuery(), ct));

    [HasPermission(Permissions.Experiments.Create)]
    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateExperimentRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(
            new CreateExperimentCommand(request.Name, request.Type, request.SubjectId, request.Description), ct);
        return CreatedAtAction(nameof(List), new { id }, new { id });
    }

    [HasPermission(Permissions.Admin.DashboardView)]
    [HttpGet("{id:long}/runs")]
    public async Task<ActionResult<IReadOnlyList<ExperimentRunDto>>> Runs(long id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetExperimentRunsQuery(id), ct));

    [HasPermission(Permissions.Experiments.Create)]
    [HttpPost("{id:long}/runs")]
    public async Task<ActionResult<object>> CreateRuns(long id, CreateRunsRequest request, CancellationToken ct)
    {
        var count = await mediator.Send(
            new CreateRunsCommand(id, request.EmbeddingModelIds, request.ChunkingStrategyIds, request.LlmModelIds), ct);
        return Ok(new { created = count });
    }

    [HasPermission(Permissions.Experiments.Run)]
    [HttpPost("{id:long}/start")]
    public async Task<ActionResult<object>> Start(long id, CancellationToken ct)
    {
        var count = await mediator.Send(new StartExperimentCommand(id), ct);
        return Accepted(new { started = count });
    }

    [HasPermission(Permissions.Admin.DashboardView)]
    [HttpGet("{id:long}/dashboard")]
    public async Task<ActionResult<IReadOnlyList<RunMetricDto>>> Dashboard(long id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetExperimentDashboardQuery(id), ct));

    [HasPermission(Permissions.Admin.DashboardView)]
    [HttpGet("runs/{runId:long}/results")]
    public async Task<ActionResult<IReadOnlyList<EvaluationResultDto>>> Results(long runId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetRunResultsQuery(runId), ct));

    [HasPermission(Permissions.Admin.DashboardView)]
    [HttpGet("/api/v1/embedding-models")]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> EmbeddingModels(CancellationToken ct) =>
        Ok(await mediator.Send(new ListEmbeddingModelsQuery(), ct));

    [HasPermission(Permissions.Admin.DashboardView)]
    [HttpGet("/api/v1/chunking-strategies")]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> ChunkingStrategies(CancellationToken ct) =>
        Ok(await mediator.Send(new ListChunkingStrategiesQuery(), ct));

    [HasPermission(Permissions.Admin.DashboardView)]
    [HttpGet("/api/v1/llm-models")]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> LlmModels(CancellationToken ct) =>
        Ok(await mediator.Send(new ListLlmModelsQuery(), ct));
}
