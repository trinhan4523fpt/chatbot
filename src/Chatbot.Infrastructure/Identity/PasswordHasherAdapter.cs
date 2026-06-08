using Chatbot.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using AppResult = Chatbot.Application.Common.Interfaces.PasswordVerificationResult;
using IdentityResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace Chatbot.Infrastructure.Identity;

/// <summary>Wraps ASP.NET Core's PBKDF2 PasswordHasher behind the application port.</summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object Dummy = new();

    public string Hash(string password) => _hasher.HashPassword(Dummy, password);

    public AppResult Verify(string hashedPassword, string providedPassword) =>
        _hasher.VerifyHashedPassword(Dummy, hashedPassword, providedPassword) switch
        {
            IdentityResult.Success => AppResult.Success,
            IdentityResult.SuccessRehashNeeded => AppResult.SuccessRehashNeeded,
            _ => AppResult.Failed,
        };
}
