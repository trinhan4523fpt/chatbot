using Chatbot.Api.Authorization;
using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Application.Features.Chat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
public sealed class ChatController(ISender mediator, IRagChatService rag, ICurrentUser currentUser) : ControllerBase
{
    [HasPermission(Permissions.Chat.CreateSession)]
    [HttpPost("sessions")]
    public async Task<ActionResult<object>> CreateSession(CreateChatSessionRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateChatSessionCommand(request.SubjectId, request.Title), ct);
        return CreatedAtAction(nameof(ListSessions), new { id }, new { id });
    }

    [HasPermission(Permissions.Chat.ReadSession)]
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<ChatSessionDto>>> ListSessions(CancellationToken ct) =>
        Ok(await mediator.Send(new ListChatSessionsQuery(), ct));

    [HasPermission(Permissions.Chat.ReadSession)]
    [HttpGet("sessions/{id:long}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetMessages(long id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetChatMessagesQuery(id), ct));

    /// <summary>Non-streaming REST fallback for sending a chat message (streaming is via /hubs/chat).</summary>
    [HasPermission(Permissions.Chat.SendMessage)]
    [HttpPost("sessions/{id:long}/messages")]
    public async Task<ActionResult<ChatAnswerResult>> Send(long id, SendMessageRequest request, CancellationToken ct)
    {
        var result = await rag.AnswerAsync(
            id, currentUser.UserId!.Value, currentUser.Roles, request.Content, _ => Task.CompletedTask, ct);
        return Ok(result);
    }

    [Authorize]
    [HttpDelete("sessions/{id:long}")]
    public async Task<IActionResult> DeleteSession(long id, CancellationToken ct)
    {
        await mediator.Send(new DeleteChatSessionCommand(id), ct);
        return NoContent();
    }
}
