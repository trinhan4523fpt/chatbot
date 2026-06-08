using Chatbot.Domain.Entities;

namespace Chatbot.Domain.Tests;

public class RefreshTokenTests
{
    [Fact]
    public void IsActive_True_WhenNotRevokedAndNotExpired()
    {
        var token = new RefreshToken { ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
        Assert.True(token.IsActive);
    }

    [Fact]
    public void IsActive_False_WhenRevoked()
    {
        var token = new RefreshToken { ExpiresAtUtc = DateTime.UtcNow.AddDays(1), RevokedAtUtc = DateTime.UtcNow };
        Assert.False(token.IsActive);
    }

    [Fact]
    public void IsActive_False_WhenExpired()
    {
        var token = new RefreshToken { ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        Assert.False(token.IsActive);
    }

    [Fact]
    public void NewUser_DefaultsToMustChangePasswordAndHasSecurityStamp()
    {
        var user = new User();
        Assert.True(user.MustChangePassword);
        Assert.True(user.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(user.SecurityStamp));
    }
}
