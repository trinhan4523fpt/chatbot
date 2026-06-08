using Chatbot.Application.Common.Authorization;

namespace Chatbot.Application.Tests;

public class PermissionMatrixTests
{
    [Fact]
    public void RoleMap_OnlyReferencesDefinedPermissions()
    {
        var defined = Permissions.All.Select(p => p.Code).ToHashSet();
        var referenced = RoleDefinitions.DefaultPermissions.Values.SelectMany(x => x).ToHashSet();
        var undefined = referenced.Except(defined).ToList();
        Assert.True(undefined.Count == 0, $"Undefined permissions referenced: {string.Join(", ", undefined)}");
    }

    [Fact]
    public void Admin_HasAllPermissions()
    {
        var all = Permissions.All.Select(p => p.Code).ToHashSet();
        var admin = RoleDefinitions.DefaultPermissions[RoleDefinitions.Admin].ToHashSet();
        Assert.True(all.SetEquals(admin));
    }

    [Fact]
    public void Student_CannotUploadDocuments_ButCanRead()
    {
        var student = RoleDefinitions.DefaultPermissions[RoleDefinitions.Student].ToHashSet();
        Assert.DoesNotContain(Permissions.Documents.Upload, student);
        Assert.Contains(Permissions.Documents.Read, student);
        Assert.Contains(Permissions.Chat.SendMessage, student);
    }

    [Fact]
    public void AllPermissionCodes_AreUnique()
    {
        var codes = Permissions.All.Select(p => p.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }
}
