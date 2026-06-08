using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Auth;

public sealed record ChangePasswordCommand(long UserId, string CurrentPassword, string NewPassword)
    : IRequest<Unit>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(256)
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số.");
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword)
            .WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
    }
}

public sealed class ChangePasswordCommandHandler(IAppDbContext db, IPasswordHasher hasher, IClock clock)
    : IRequestHandler<ChangePasswordCommand, Unit>
{
    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        if (hasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            throw new Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["Mật khẩu hiện tại không đúng."],
            });
        }

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.SecurityStamp = Guid.NewGuid().ToString("N"); // revokes existing access tokens within the cache SLA

        // Revoke all refresh tokens -> force re-login on other devices.
        var active = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var token in active)
        {
            token.RevokedAtUtc = clock.UtcNow;
            token.ReasonRevoked = "password_changed";
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = "PasswordChanged", ActorUserId = user.Id, ActorEmail = user.Email, TargetUserId = user.Id,
        });

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
