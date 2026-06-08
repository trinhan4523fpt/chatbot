namespace Chatbot.Application.Common.Interfaces;

/// <summary>Abstraction over the system clock (UTC) for testability.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
