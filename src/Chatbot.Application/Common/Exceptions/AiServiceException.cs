namespace Chatbot.Application.Common.Exceptions;

/// <summary>
/// Raised when the Python ML service returns a non-success status. 4xx is a permanent data/client
/// error (do not retry); 5xx / network failures are transient (safe to retry).
/// </summary>
public sealed class AiServiceException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public bool IsPermanent => StatusCode is >= 400 and < 500;
}
