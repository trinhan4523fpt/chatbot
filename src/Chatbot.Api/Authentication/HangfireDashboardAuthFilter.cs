using Hangfire.Dashboard;

namespace Chatbot.Api.Authentication;

/// <summary>
/// Guards the Hangfire dashboard. For now it allows access only in Development; production
/// hardening (admin.jobs.view behind a proper auth scheme) is handled in M6.
/// </summary>
public sealed class HangfireDashboardAuthFilter(bool allowAll) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => allowAll;
}
