namespace Chatbot.Application.Features.Auth;

public sealed record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    bool MustChangePassword);

public sealed record CurrentUserDto(
    long Id,
    string Email,
    string FullName,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
