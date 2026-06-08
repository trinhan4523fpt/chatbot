using Chatbot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Chatbot.Api.Authentication;

/// <summary>
/// Validates a token's user is still active and its security stamp current, cached briefly.
/// Cache is keyed by (userId, stamp): a freshly-issued token (new stamp) always gets a correct
/// fresh read, while a revoked token (old stamp) stays "valid" only until the TTL — the
/// effective revocation SLA for password changes / deactivation / role edits.
/// </summary>
public sealed class SecurityStampService(IServiceScopeFactory scopeFactory, IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public async Task<bool> IsValidAsync(long userId, string securityStamp)
    {
        return await cache.GetOrCreateAsync($"user-sec:{userId}:{securityStamp}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();
            var snapshot = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsActive, u.SecurityStamp })
                .FirstOrDefaultAsync();
            return snapshot is not null && snapshot.IsActive && snapshot.SecurityStamp == securityStamp;
        });
    }
}
