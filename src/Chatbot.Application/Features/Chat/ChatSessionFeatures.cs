using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Chat;

// ---- Create session ------------------------------------------------------------
public sealed record CreateChatSessionCommand(long SubjectId, string? Title) : IRequest<long>;

public sealed class CreateChatSessionCommandValidator : AbstractValidator<CreateChatSessionCommand>
{
    public CreateChatSessionCommandValidator()
    {
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.Title).MaximumLength(255);
    }
}

public sealed class CreateChatSessionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateChatSessionCommand, long>
{
    public async Task<long> Handle(CreateChatSessionCommand request, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == request.SubjectId, ct))
        {
            throw new NotFoundException("Không tìm thấy môn học.");
        }

        var session = new ChatSession
        {
            UserId = currentUser.UserId!.Value,
            SubjectId = request.SubjectId,
            Title = request.Title,
        };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session.Id;
    }
}

// ---- List my sessions ----------------------------------------------------------
public sealed record ListChatSessionsQuery : IRequest<IReadOnlyList<ChatSessionDto>>;

public sealed class ListChatSessionsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListChatSessionsQuery, IReadOnlyList<ChatSessionDto>>
{
    public async Task<IReadOnlyList<ChatSessionDto>> Handle(ListChatSessionsQuery request, CancellationToken ct) =>
        await db.ChatSessions.AsNoTracking()
            .Where(s => s.UserId == currentUser.UserId!.Value)
            .OrderByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc)
            .Select(s => new ChatSessionDto(s.Id, s.SubjectId, s.Title, s.CreatedAtUtc, s.UpdatedAtUtc))
            .ToListAsync(ct);
}

// ---- Get session messages ------------------------------------------------------
public sealed record GetChatMessagesQuery(long SessionId) : IRequest<IReadOnlyList<ChatMessageDto>>;

public sealed class GetChatMessagesQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetChatMessagesQuery, IReadOnlyList<ChatMessageDto>>
{
    public async Task<IReadOnlyList<ChatMessageDto>> Handle(GetChatMessagesQuery request, CancellationToken ct)
    {
        var session = await db.ChatSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SessionId, ct)
            ?? throw new NotFoundException("Không tìm thấy phiên chat.");

        var canReadAny = currentUser.Roles.Contains(RoleDefinitions.Admin)
                         || currentUser.Roles.Contains(RoleDefinitions.Lecturer);
        if (session.UserId != currentUser.UserId && !canReadAny)
        {
            throw new ForbiddenException("Bạn không có quyền xem phiên chat này.");
        }

        return await db.ChatMessages.AsNoTracking()
            .Where(m => m.SessionId == request.SessionId)
            .OrderBy(m => m.Id)
            .Select(m => new ChatMessageDto(
                m.Id, m.Role, m.Content, m.Status, m.LatencyMs, m.CreatedAtUtc,
                m.Citations.Select(c => new ChatCitationDto(
                    c.ChunkId, c.DocumentId, c.DocumentTitle, c.RelevanceScore, c.Snippet, c.PageNumber)).ToList()))
            .ToListAsync(ct);
    }
}

// ---- Delete session ------------------------------------------------------------
public sealed record DeleteChatSessionCommand(long SessionId) : IRequest<Unit>;

public sealed class DeleteChatSessionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteChatSessionCommand, Unit>
{
    public async Task<Unit> Handle(DeleteChatSessionCommand request, CancellationToken ct)
    {
        var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == request.SessionId, ct)
            ?? throw new NotFoundException("Không tìm thấy phiên chat.");

        var canDeleteAny = currentUser.Roles.Contains(RoleDefinitions.Admin);
        if (session.UserId != currentUser.UserId && !canDeleteAny)
        {
            throw new ForbiddenException("Bạn không có quyền xóa phiên chat này.");
        }

        db.ChatSessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
