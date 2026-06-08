namespace Chatbot.Application.Common.Exceptions;

/// <summary>Base for expected application errors that map to HTTP status codes.</summary>
public abstract class AppException(string message) : Exception(message);

/// <summary>404 — a requested resource does not exist.</summary>
public sealed class NotFoundException(string message) : AppException(message);

/// <summary>409 — the request conflicts with current state (e.g. duplicate).</summary>
public sealed class ConflictException(string message) : AppException(message);

/// <summary>403 — authenticated but not allowed.</summary>
public sealed class ForbiddenException(string message = "You do not have permission to perform this action.")
    : AppException(message);

/// <summary>401 — authentication failed or required.</summary>
public sealed class UnauthorizedException(string message = "Unauthorized.") : AppException(message);

/// <summary>422-style domain rule violation (business invariant).</summary>
public sealed class BusinessRuleException(string message) : AppException(message);

/// <summary>400 — input validation failed. Carries per-field errors.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
