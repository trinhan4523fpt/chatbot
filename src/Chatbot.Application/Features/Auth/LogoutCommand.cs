using Chatbot.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Auth;

public sealed record LogoutCommand(long UserId, string? IpAddress) : IRequest<Unit>;

public sealed class LogoutCommandHandler(IAppDbContext db, IClock clock)
    : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken ct)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == request.UserId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in active)
        {
            token.RevokedAtUtc = clock.UtcNow;
            token.RevokedByIp = request.IpAddress;
            token.ReasonRevoked = "logout";
        }

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
