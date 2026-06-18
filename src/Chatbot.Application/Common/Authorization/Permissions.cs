namespace Chatbot.Application.Common.Authorization;

public sealed record PermissionDef(string Code, string Name, string Category);

/// <summary>Canonical permission codes. Used by [HasPermission] and by the seeder.</summary>
public static class Permissions
{
    public static class Users
    {
        public const string Create = "users.create";
        public const string Read = "users.read";
        public const string Update = "users.update";
        public const string AssignRole = "users.assign_role";
        public const string ResetPassword = "users.reset_password";
    }

    public static class Profile
    {
        public const string ReadSelf = "profile.read_self";
        public const string ChangePasswordSelf = "profile.change_password_self";
    }

    public static class Catalog
    {
        public const string SubjectsManage = "subjects.manage";
        public const string ChaptersManage = "chapters.manage";
        public const string AssignInstructor = "subjects.assign_instructor";
    }

    public static class Documents
    {
        public const string Upload = "documents.upload";
        public const string Read = "documents.read";
        public const string ReadChunks = "documents.read_chunks";
        public const string Download = "documents.download";
        public const string Delete = "documents.delete";
        public const string Reindex = "documents.reindex";
    }

    public static class Chat
    {
        public const string CreateSession = "chat.create_session";
        public const string SendMessage = "chat.send_message";
        public const string ReadSession = "chat.read_session";
        public const string ReadAny = "chat.read_any";
        public const string DeleteAny = "chat.delete_any";
    }

    public static class Experiments
    {
        public const string Create = "experiments.create";
        public const string Run = "experiments.run";
        public const string ManageCatalog = "experiments.manage_catalog";
        public const string ManageTestset = "experiments.manage_testset";
        public const string Finetune = "experiments.finetune";
        public const string Export = "experiments.export";
    }

    public static class Admin
    {
        public const string DashboardView = "dashboard.view";
        public const string JobsView = "admin.jobs.view";
        public const string Config = "admin.config";
        public const string AuditView = "admin.audit.view";
    }

    /// <summary>Every permission with display metadata, for seeding.</summary>
    public static readonly IReadOnlyList<PermissionDef> All =
    [
        new(Users.Create, "Create users", "Users"),
        new(Users.Read, "Read users", "Users"),
        new(Users.Update, "Update users", "Users"),
        new(Users.AssignRole, "Assign roles", "Users"),
        new(Users.ResetPassword, "Reset user password", "Users"),
        new(Profile.ReadSelf, "Read own profile", "Profile"),
        new(Profile.ChangePasswordSelf, "Change own password", "Profile"),
        new(Catalog.SubjectsManage, "Manage subjects", "Catalog"),
        new(Catalog.ChaptersManage, "Manage chapters", "Catalog"),
        new(Catalog.AssignInstructor, "Assign instructor to subject", "Catalog"),
        new(Documents.Upload, "Upload documents", "Documents"),
        new(Documents.Read, "Read documents", "Documents"),
        new(Documents.ReadChunks, "Read document chunks", "Documents"),
        new(Documents.Download, "Download documents", "Documents"),
        new(Documents.Delete, "Delete documents", "Documents"),
        new(Documents.Reindex, "Re-index documents", "Documents"),
        new(Chat.CreateSession, "Create chat session", "Chat"),
        new(Chat.SendMessage, "Send chat message", "Chat"),
        new(Chat.ReadSession, "Read own chat sessions", "Chat"),
        new(Chat.ReadAny, "Read any chat session", "Chat"),
        new(Chat.DeleteAny, "Delete any chat session", "Chat"),
        new(Experiments.Create, "Create experiments", "Experiments"),
        new(Experiments.Run, "Run experiments", "Experiments"),
        new(Experiments.ManageCatalog, "Manage model/strategy catalog", "Experiments"),
        new(Experiments.ManageTestset, "Manage test questions", "Experiments"),
        new(Experiments.Finetune, "Fine-tune models", "Experiments"),
        new(Experiments.Export, "Export experiment results", "Experiments"),
        new(Admin.DashboardView, "View dashboard", "Admin"),
        new(Admin.JobsView, "View background jobs", "Admin"),
        new(Admin.Config, "Manage system configuration", "Admin"),
        new(Admin.AuditView, "View audit log", "Admin"),
    ];
}

/// <summary>Seeded role names and their default permission sets.</summary>
public static class RoleDefinitions
{
    public const string Admin = "Admin";
    public const string Researcher = "Researcher";
    public const string Instructor = "Instructor";
    public const string Student = "Student";

    public static readonly IReadOnlyList<string> AllRoles = [Admin, Researcher, Instructor, Student];

    private static readonly string[] ProfileSelf =
        [Permissions.Profile.ReadSelf, Permissions.Profile.ChangePasswordSelf];

    /// <summary>Role -> permission codes. Admin gets everything.</summary>
    public static IReadOnlyDictionary<string, string[]> DefaultPermissions { get; } =
        new Dictionary<string, string[]>
        {
            [Admin] = [.. Permissions.All.Select(p => p.Code)],
            [Researcher] =
            [
                Permissions.Catalog.SubjectsManage, Permissions.Catalog.ChaptersManage,
                Permissions.Documents.Upload, Permissions.Documents.Read, Permissions.Documents.ReadChunks,
                Permissions.Documents.Download, Permissions.Documents.Delete, Permissions.Documents.Reindex,
                Permissions.Chat.CreateSession, Permissions.Chat.SendMessage, Permissions.Chat.ReadSession,
                Permissions.Chat.ReadAny,
                Permissions.Experiments.Create, Permissions.Experiments.Run, Permissions.Experiments.ManageCatalog,
                Permissions.Experiments.ManageTestset, Permissions.Experiments.Finetune, Permissions.Experiments.Export,
                Permissions.Admin.DashboardView,
                .. ProfileSelf,
            ],
            [Instructor] =
            [
                Permissions.Catalog.ChaptersManage,
                Permissions.Documents.Upload, Permissions.Documents.Read, Permissions.Documents.ReadChunks,
                Permissions.Documents.Download, Permissions.Documents.Delete, Permissions.Documents.Reindex,
                Permissions.Chat.CreateSession, Permissions.Chat.SendMessage, Permissions.Chat.ReadSession,
                .. ProfileSelf,
            ],
            [Student] =
            [
                Permissions.Documents.Read, Permissions.Documents.Download,
                Permissions.Chat.CreateSession, Permissions.Chat.SendMessage, Permissions.Chat.ReadSession,
                .. ProfileSelf,
            ],
        };
}
