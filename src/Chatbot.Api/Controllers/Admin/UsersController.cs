using Chatbot.Api.Authorization;
using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Models;
using Chatbot.Application.Features.Admin.Users;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/users")]
public sealed class UsersController(ISender mediator) : ControllerBase
{
    [HasPermission(Permissions.Users.Read)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new ListUsersQuery(page, pageSize, search), ct));

    [HasPermission(Permissions.Users.Create)]
    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(
            new CreateUserCommand(request.Email, request.FullName, request.Password, request.Roles ?? []), ct);
        return CreatedAtAction(nameof(List), new { id }, new { id });
    }

    [HasPermission(Permissions.Users.AssignRole)]
    [HttpPut("{id:long}/roles")]
    public async Task<IActionResult> AssignRoles(long id, AssignRolesRequest request, CancellationToken ct)
    {
        await mediator.Send(new AssignRolesCommand(id, request.Roles), ct);
        return NoContent();
    }

    [HasPermission(Permissions.Users.ResetPassword)]
    [HttpPost("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, ResetPasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(new ResetUserPasswordCommand(id, request.NewPassword), ct);
        return NoContent();
    }

    [HasPermission(Permissions.Users.Update)]
    [HttpPost("{id:long}/active")]
    public async Task<IActionResult> SetActive(long id, SetActiveRequest request, CancellationToken ct)
    {
        await mediator.Send(new SetUserActiveCommand(id, request.IsActive), ct);
        return NoContent();
    }
}
