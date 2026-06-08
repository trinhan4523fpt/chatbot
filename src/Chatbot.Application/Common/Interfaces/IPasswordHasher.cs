namespace Chatbot.Application.Common.Interfaces;

public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

/// <summary>Password hashing abstraction (implemented over ASP.NET Core's PasswordHasher).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerificationResult Verify(string hashedPassword, string providedPassword);
}
