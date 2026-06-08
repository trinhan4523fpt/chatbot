using Chatbot.Api.Authorization;
using Chatbot.Application.Common.Authorization;

namespace Chatbot.Api.Tests;

public class PermissionAttributeTests
{
    [Fact]
    public void HasPermission_SetsPrefixedPolicy()
    {
        var attribute = new HasPermissionAttribute(Permissions.Documents.Upload);
        Assert.Equal("PERM:documents.upload", attribute.Policy);
    }

    [Fact]
    public async Task PermissionPolicyProvider_BuildsPolicy_ForPermPolicies()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Authorization.AuthorizationOptions());
        var provider = new PermissionPolicyProvider(options);

        var policy = await provider.GetPolicyAsync("PERM:documents.read");

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is PermissionRequirement);
    }
}
