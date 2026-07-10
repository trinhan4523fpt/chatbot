namespace Chatbot.Application.Features.Auth;

public sealed record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken);

public sealed record CurrentUserDto(
    long Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
