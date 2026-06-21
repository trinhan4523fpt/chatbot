using Chatbot.Api.Authorization;
using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Features.Catalog;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/subjects")]
public sealed class CatalogController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubjectDto>>> ListSubjects(
        [FromQuery] bool mine = false, CancellationToken ct = default) =>
        Ok(await mediator.Send(new ListSubjectsQuery(mine), ct));

    [HasPermission(Permissions.Catalog.SubjectsManage)]
    [HttpPost]
    public async Task<ActionResult<object>> CreateSubject(CreateSubjectRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateSubjectCommand(request.Code, request.Name, request.Description), ct);
        return CreatedAtAction(nameof(ListSubjects), new { id }, new { id });
    }

    [HasPermission(Permissions.Catalog.SubjectsManage)]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateSubject(long id, UpdateSubjectRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateSubjectCommand(id, request.Name, request.Description), ct);
        return NoContent();
    }

    [HttpGet("{subjectId:long}/chapters")]
    public async Task<ActionResult<IReadOnlyList<ChapterDto>>> ListChapters(long subjectId, CancellationToken ct) =>
        Ok(await mediator.Send(new ListChaptersQuery(subjectId), ct));

    [HasPermission(Permissions.Catalog.ChaptersManage)]
    [HttpPost("{subjectId:long}/chapters")]
    public async Task<ActionResult<object>> CreateChapter(
        long subjectId, CreateChapterRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateChapterCommand(subjectId, request.Title, request.OrderIndex), ct);
        return CreatedAtAction(nameof(ListChapters), new { subjectId }, new { id });
    }

    // ---- Instructor assignment (Admin) -----------------------------------------
    [HttpGet("{subjectId:long}/instructors")]
    public async Task<ActionResult<IReadOnlyList<SubjectInstructorDto>>> ListInstructors(
        long subjectId, CancellationToken ct) =>
        Ok(await mediator.Send(new ListSubjectInstructorsQuery(subjectId), ct));

    [HasPermission(Permissions.Catalog.AssignInstructor)]
    [HttpPost("{subjectId:long}/instructors")]
    public async Task<IActionResult> AssignInstructor(
        long subjectId, AssignInstructorRequest request, CancellationToken ct)
    {
        await mediator.Send(new AssignInstructorCommand(subjectId, request.UserId), ct);
        return NoContent();
    }

    [HasPermission(Permissions.Catalog.AssignInstructor)]
    [HttpDelete("{subjectId:long}/instructors/{userId:long}")]
    public async Task<IActionResult> UnassignInstructor(long subjectId, long userId, CancellationToken ct)
    {
        await mediator.Send(new UnassignInstructorCommand(subjectId, userId), ct);
        return NoContent();
    }
}
