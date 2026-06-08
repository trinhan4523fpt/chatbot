using Chatbot.Api.Authorization;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Features.Admin.Roles;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/roles")]
public sealed class RolesController(ISender mediator) : ControllerBase
{
    [HasPermission(Permissions.Users.Read)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> List(CancellationToken ct) =>
        Ok(await mediator.Send(new ListRolesQuery(), ct));
}
