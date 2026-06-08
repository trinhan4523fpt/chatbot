using System.Security.Claims;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Infrastructure.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Chatbot.Api.Authentication;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public long? UserId =>
        long.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    public string? Email => Principal?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(JwtTokenService.RoleClaim).Select(c => c.Value).ToArray() ?? [];
}
