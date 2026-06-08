using Chatbot.Application.Common.Interfaces;
using Chatbot.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Chatbot.Api.Authorization;

/// <summary>Requires the caller to hold a specific permission, e.g. [HasPermission(Permissions.Documents.Upload)].</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "PERM:";

    public HasPermissionAttribute(string permission) => Policy = PolicyPrefix + permission;
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Builds PERM:* policies on demand; delegates everything else to the default provider.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}

public sealed class PermissionAuthorizationHandler(IPermissionResolver resolver)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.FindAll(JwtTokenService.RoleClaim).Select(c => c.Value).ToArray();
        if (roles.Length == 0)
        {
            return;
        }

        var permissions = await resolver.GetPermissionsForRolesAsync(roles);
        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>Resolves the effective permission set for a set of roles, cached per role (short TTL = revocation SLA).</summary>
public interface IPermissionResolver
{
    Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(IReadOnlyCollection<string> roleNames);
}

public sealed class PermissionResolver(IAppDbContext db, IMemoryCache cache) : IPermissionResolver
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(IReadOnlyCollection<string> roleNames)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleName in roleNames)
        {
            var perms = await cache.GetOrCreateAsync($"role-perms:{roleName}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Ttl;
                var normalized = roleName.ToUpperInvariant();
                return await db.RolePermissions
                    .Where(rp => rp.Role.NormalizedName == normalized)
                    .Select(rp => rp.Permission.Code)
                    .ToArrayAsync();
            }) ?? [];

            foreach (var p in perms)
            {
                result.Add(p);
            }
        }

        return result;
    }
}
