using Chatbot.Infrastructure.Identity;

namespace Chatbot.Api.Authentication;

/// <summary>
/// When the caller's token carries pwd_change_required, blocks every endpoint except
/// changing the password, reading own profile, and logging out.
/// </summary>
public sealed class PasswordChangeGateMiddleware(RequestDelegate next)
{
    private static readonly string[] Allowed =
    [
        "/api/v1/auth/change-password",
        "/api/v1/auth/me",
        "/api/v1/auth/logout",
    ];

    public async Task Invoke(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true &&
            user.HasClaim(JwtTokenService.PasswordChangeRequiredClaim, "true"))
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var allowed = Allowed.Any(a => path.StartsWith(a, StringComparison.OrdinalIgnoreCase));
            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Password change required",
                    status = 403,
                    detail = "Bạn cần đổi mật khẩu trước khi tiếp tục.",
                    code = "password_change_required",
                });
                return;
            }
        }

        await next(context);
    }
}
