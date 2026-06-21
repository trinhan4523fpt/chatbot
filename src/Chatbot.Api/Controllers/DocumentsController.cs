using Chatbot.Api.Authorization;
using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Models;
using Chatbot.Application.Features.Documents;
using Chatbot.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/v1/documents")]
public sealed class DocumentsController(ISender mediator) : ControllerBase
{
    [HasPermission(Permissions.Documents.Read)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<DocumentDto>>> List(
        [FromQuery] long? subjectId, [FromQuery] DocumentStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await mediator.Send(new ListDocumentsQuery(subjectId, status, page, pageSize), ct));

    [HasPermission(Permissions.Documents.Read)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<DocumentDto>> Get(long id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetDocumentQuery(id), ct));

    [HasPermission(Permissions.Documents.Upload)]
    [HttpPost]
    [RequestSizeLimit(60_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60_000_000)]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] UploadDocumentForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        var dto = await mediator.Send(
            new UploadDocumentCommand(stream, form.File.FileName, form.File.ContentType, form.SubjectId, form.ChapterId, form.Title),
            ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HasPermission(Permissions.Documents.Download)]
    [HttpGet("{id:long}/download")]
    public async Task<IActionResult> Download(long id, CancellationToken ct)
    {
        var info = await mediator.Send(new DownloadDocumentQuery(id), ct);
        return PhysicalFile(info.PhysicalPath, info.ContentType, info.OriginalFileName);
    }

    [HasPermission(Permissions.Documents.Delete)]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await mediator.Send(new DeleteDocumentCommand(id), ct);
        return NoContent();
    }

    [HasPermission(Permissions.Documents.Reindex)]
    [HttpPost("{id:long}/reindex")]
    public async Task<IActionResult> Reindex(long id, CancellationToken ct)
    {
        await mediator.Send(new ReindexDocumentCommand(id), ct);
        return Accepted();
    }

    [HasPermission(Permissions.Documents.Read)]
    [HttpGet("{id:long}/status")]
    public async Task<ActionResult<DocumentStatusDto>> Status(long id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetDocumentStatusQuery(id), ct));

    [HasPermission(Permissions.Documents.ReadChunks)]
    [HttpGet("{id:long}/chunks")]
    public async Task<ActionResult<PagedResult<DocumentChunkDto>>> Chunks(
        long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetDocumentChunksQuery(id, page, pageSize), ct));
}
