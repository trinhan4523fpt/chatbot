namespace Chatbot.Application.Common.Interfaces;

/// <summary>The authenticated user for the current request (or none).</summary>
public interface ICurrentUser
{
    long? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }
}
