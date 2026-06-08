using Chatbot.Api.Authentication;
using Chatbot.Api.Authorization;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Features.Chat;
using Chatbot.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Chatbot.Api.Hubs;

[Authorize]
public sealed class ChatHub(IRagChatService rag, SecurityStampService security) : Hub
{
    /// <summary>Streams a grounded answer: client receives ReceiveToken* then ReceiveComplete (or Error).</summary>
    [HasPermission(Permissions.Chat.SendMessage)]
    public async Task SendMessage(long sessionId, string question)
    {
        var user = Context.User!;

        if (user.HasClaim(JwtTokenService.PasswordChangeRequiredClaim, "true"))
        {
            await Clients.Caller.SendAsync("Error", "password_change_required");
            return;
        }

        var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var stamp = user.FindFirst(JwtTokenService.SecurityStampClaim)?.Value;
        if (!long.TryParse(sub, out var userId) || stamp is null || !await security.IsValidAsync(userId, stamp))
        {
            Context.Abort();
            return;
        }

        var roles = user.FindAll(JwtTokenService.RoleClaim).Select(c => c.Value).ToArray();
        var ct = Context.ConnectionAborted;

        try
        {
            var result = await rag.AnswerAsync(
                sessionId, userId, roles, question,
                async delta => await Clients.Caller.SendAsync("ReceiveToken", delta, ct),
                ct);
            await Clients.Caller.SendAsync("ReceiveComplete", result, ct);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected mid-stream; partial answer already persisted as cancelled.
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message, CancellationToken.None);
        }
    }
}
