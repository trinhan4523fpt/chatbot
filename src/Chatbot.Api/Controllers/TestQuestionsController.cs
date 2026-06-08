using Chatbot.Api.Authorization;
using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Features.Experiments;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/v1/test-questions")]
public sealed class TestQuestionsController(ISender mediator) : ControllerBase
{
    [HasPermission(Permissions.Admin.DashboardView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TestQuestionDto>>> List(
        [FromQuery] long? subjectId, CancellationToken ct) =>
        Ok(await mediator.Send(new ListTestQuestionsQuery(subjectId), ct));

    [HasPermission(Permissions.Experiments.ManageTestset)]
    [HttpPost("import")]
    public async Task<ActionResult<object>> Import(ImportTestQuestionsRequest request, CancellationToken ct)
    {
        var items = request.Items
            .Select(i => new TestQuestionImportItem(i.Question, i.GroundTruth, i.ReferenceContext, i.Difficulty, i.ExternalRef))
            .ToList();
        var added = await mediator.Send(new ImportTestQuestionsCommand(request.SubjectId, items), ct);
        return Ok(new { imported = added });
    }
}
