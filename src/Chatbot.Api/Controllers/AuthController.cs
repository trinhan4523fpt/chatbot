using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender mediator, ICurrentUser currentUser) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new LoginCommand(request.Email, request.Password, Ip()), ct));

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RefreshTokenCommand(request.RefreshToken, Ip()), ct));

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await mediator.Send(new LogoutCommand(currentUser.UserId!.Value, Ip()), ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(
            new ChangePasswordCommand(currentUser.UserId!.Value, request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct) =>
        Ok(await mediator.Send(new GetMeQuery(currentUser.UserId!.Value), ct));

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
