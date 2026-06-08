using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.EnsureSchema(
                name: "rag");

            migrationBuilder.EnsureSchema(
                name: "rbl");

            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.CreateTable(
                name: "AppSetting",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSetting", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetUserId = table.Column<long>(type: "bigint", nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                    table.CheckConstraint("CK_AuditLog_NewValues_Json", "[NewValues] IS NULL OR ISJSON([NewValues]) = 1");
                    table.CheckConstraint("CK_AuditLog_OldValues_Json", "[OldValues] IS NULL OR ISJSON([OldValues]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "ChunkingStrategy",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChunkSize = table.Column<int>(type: "int", nullable: true),
                    ChunkOverlap = table.Column<int>(type: "int", nullable: true),
                    Params = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkingStrategy", x => x.Id);
                    table.CheckConstraint("CK_ChunkingStrategy_Params_Json", "[Params] IS NULL OR ISJSON([Params]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "EmbeddingModel",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Dimension = table.Column<int>(type: "int", nullable: false),
                    IsFree = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxInputTokens = table.Column<int>(type: "int", nullable: true),
                    QdrantCollectionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationOutbox",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    AvailableAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOutbox", x => x.Id);
                    table.CheckConstraint("CK_Outbox_Payload_Json", "[Payload] IS NULL OR ISJSON([Payload]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "LlmModel",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BaseModel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subject",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Vietnamese_100_CI_AI"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subject", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemConfiguration",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ActiveEmbeddingModelId = table.Column<long>(type: "bigint", nullable: true),
                    ActiveChunkingStrategyId = table.Column<long>(type: "bigint", nullable: true),
                    ActiveLlmModelId = table.Column<long>(type: "bigint", nullable: true),
                    RetrievalTopK = table.Column<int>(type: "int", nullable: false),
                    MinRelevanceScore = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: false),
                    ScopeRestriction = table.Column<bool>(type: "bit", nullable: false),
                    PromptTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxUploadBytes = table.Column<long>(type: "bigint", nullable: false),
                    HistoryWindowTurns = table.Column<int>(type: "int", nullable: false),
                    LockoutMaxAttempts = table.Column<int>(type: "int", nullable: false),
                    LockoutMinutes = table.Column<int>(type: "int", nullable: false),
                    AccessTokenMinutes = table.Column<int>(type: "int", nullable: false),
                    RefreshTokenDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfiguration", x => x.Id);
                    table.CheckConstraint("CK_SystemConfiguration_Singleton", "[Id] = 1");
                    table.ForeignKey(
                        name: "FK_SystemConfiguration_ChunkingStrategy_ActiveChunkingStrategyId",
                        column: x => x.ActiveChunkingStrategyId,
                        principalSchema: "dbo",
                        principalTable: "ChunkingStrategy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemConfiguration_EmbeddingModel_ActiveEmbeddingModelId",
                        column: x => x.ActiveEmbeddingModelId,
                        principalSchema: "dbo",
                        principalTable: "EmbeddingModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemConfiguration_LlmModel_ActiveLlmModelId",
                        column: x => x.ActiveLlmModelId,
                        principalSchema: "dbo",
                        principalTable: "LlmModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                schema: "auth",
                columns: table => new
                {
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermission_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "auth",
                        principalTable: "Permission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermission_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Chapter",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Vietnamese_100_CI_AI"),
                    OrderIndex = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chapter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chapter_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "dbo",
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Experiment",
                schema: "rbl",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Experiment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Experiment_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "dbo",
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestQuestion",
                schema: "rbl",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<long>(type: "bigint", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GroundTruth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferenceContext = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Difficulty = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ExternalRef = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestQuestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestQuestion_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "dbo",
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatSession",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PinnedEmbeddingModelId = table.Column<long>(type: "bigint", nullable: true),
                    PinnedChunkingStrategyId = table.Column<long>(type: "bigint", nullable: true),
                    PinnedLlmModelId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatSession_ChunkingStrategy_PinnedChunkingStrategyId",
                        column: x => x.PinnedChunkingStrategyId,
                        principalSchema: "dbo",
                        principalTable: "ChunkingStrategy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatSession_EmbeddingModel_PinnedEmbeddingModelId",
                        column: x => x.PinnedEmbeddingModelId,
                        principalSchema: "dbo",
                        principalTable: "EmbeddingModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatSession_LlmModel_PinnedLlmModelId",
                        column: x => x.PinnedLlmModelId,
                        principalSchema: "dbo",
                        principalTable: "LlmModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatSession_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "dbo",
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatSession_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetToken",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "char(64)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetToken_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "char(64)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JwtId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedByIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RevokedByIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "char(64)", nullable: true, collation: "Latin1_General_100_BIN2"),
                    ReasonRevoked = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshToken_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRole",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AssignedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRole_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRole_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSubject",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: false),
                    EnrolledAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubject", x => new { x.UserId, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_UserSubject_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "dbo",
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubject_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Document",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<long>(type: "bigint", nullable: false),
                    ChapterId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, collation: "Vietnamese_100_CI_AI"),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FileExtension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StorageProvider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "LocalDisk"),
                    Sha256Checksum = table.Column<string>(type: "char(64)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IndexedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Document", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Document_Chapter_ChapterId",
                        column: x => x.ChapterId,
                        principalSchema: "dbo",
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Document_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "dbo",
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExperimentRun",
                schema: "rbl",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExperimentId = table.Column<long>(type: "bigint", nullable: false),
                    ExperimentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmbeddingModelId = table.Column<long>(type: "bigint", nullable: true),
                    ChunkingStrategyId = table.Column<long>(type: "bigint", nullable: true),
                    LlmModelId = table.Column<long>(type: "bigint", nullable: true),
                    RunName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Params = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfigSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorpusSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HangfireJobId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentRun", x => x.Id);
                    table.CheckConstraint("CK_Run_ConfigSnapshot_Json", "[ConfigSnapshot] IS NULL OR ISJSON([ConfigSnapshot]) = 1");
                    table.CheckConstraint("CK_Run_CorpusSnapshot_Json", "[CorpusSnapshot] IS NULL OR ISJSON([CorpusSnapshot]) = 1");
                    table.CheckConstraint("CK_Run_Params_Json", "[Params] IS NULL OR ISJSON([Params]) = 1");
                    table.CheckConstraint("CK_Run_TypeModel", "([ExperimentType] = 'embedding_bench' AND [EmbeddingModelId] IS NOT NULL) OR ([ExperimentType] = 'chunking_bench' AND [ChunkingStrategyId] IS NOT NULL) OR ([ExperimentType] = 'rag_vs_finetune' AND [LlmModelId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ExperimentRun_ChunkingStrategy_ChunkingStrategyId",
                        column: x => x.ChunkingStrategyId,
                        principalSchema: "dbo",
                        principalTable: "ChunkingStrategy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExperimentRun_EmbeddingModel_EmbeddingModelId",
                        column: x => x.EmbeddingModelId,
                        principalSchema: "dbo",
                        principalTable: "EmbeddingModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExperimentRun_Experiment_ExperimentId",
                        column: x => x.ExperimentId,
                        principalSchema: "rbl",
                        principalTable: "Experiment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExperimentRun_LlmModel_LlmModelId",
                        column: x => x.LlmModelId,
                        principalSchema: "dbo",
                        principalTable: "LlmModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessage",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    LlmModelId = table.Column<long>(type: "bigint", nullable: true),
                    EmbeddingModelId = table.Column<long>(type: "bigint", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessage_ChatSession_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "dbo",
                        principalTable: "ChatSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatMessage_EmbeddingModel_EmbeddingModelId",
                        column: x => x.EmbeddingModelId,
                        principalSchema: "dbo",
                        principalTable: "EmbeddingModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatMessage_LlmModel_LlmModelId",
                        column: x => x.LlmModelId,
                        principalSchema: "dbo",
                        principalTable: "LlmModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentChunk",
                schema: "rag",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    ChunkingStrategyId = table.Column<long>(type: "bigint", nullable: false),
                    ChunkIndex = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenCount = table.Column<int>(type: "int", nullable: true),
                    PageNumber = table.Column<int>(type: "int", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentHash = table.Column<string>(type: "char(64)", nullable: true, collation: "Latin1_General_100_BIN2"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChunk", x => x.Id);
                    table.CheckConstraint("CK_DocumentChunk_Metadata_Json", "[Metadata] IS NULL OR ISJSON([Metadata]) = 1");
                    table.ForeignKey(
                        name: "FK_DocumentChunk_ChunkingStrategy_ChunkingStrategyId",
                        column: x => x.ChunkingStrategyId,
                        principalSchema: "dbo",
                        principalTable: "ChunkingStrategy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentChunk_Document_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "Document",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentProcessingJob",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    ChunkingStrategyId = table.Column<long>(type: "bigint", nullable: true),
                    EmbeddingModelId = table.Column<long>(type: "bigint", nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    HangfireJobId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentProcessingJob", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentProcessingJob_ChunkingStrategy_ChunkingStrategyId",
                        column: x => x.ChunkingStrategyId,
                        principalSchema: "dbo",
                        principalTable: "ChunkingStrategy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentProcessingJob_Document_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "Document",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentProcessingJob_EmbeddingModel_EmbeddingModelId",
                        column: x => x.EmbeddingModelId,
                        principalSchema: "dbo",
                        principalTable: "EmbeddingModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationResult",
                schema: "rbl",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExperimentRunId = table.Column<long>(type: "bigint", nullable: false),
                    TestQuestionId = table.Column<long>(type: "bigint", nullable: false),
                    GeneratedAnswer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetrievedContexts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Faithfulness = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    AnswerRelevancy = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    ContextPrecision = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    ContextRecall = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    AnswerCorrectness = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    PerQuestionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationResult", x => x.Id);
                    table.CheckConstraint("CK_Eval_AnsCorr", "[AnswerCorrectness] IS NULL OR [AnswerCorrectness] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Eval_AnsRel", "[AnswerRelevancy] IS NULL OR [AnswerRelevancy] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Eval_Contexts_Json", "[RetrievedContexts] IS NULL OR ISJSON([RetrievedContexts]) = 1");
                    table.CheckConstraint("CK_Eval_CtxPrec", "[ContextPrecision] IS NULL OR [ContextPrecision] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Eval_CtxRec", "[ContextRecall] IS NULL OR [ContextRecall] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Eval_Faith", "[Faithfulness] IS NULL OR [Faithfulness] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_EvaluationResult_ExperimentRun_ExperimentRunId",
                        column: x => x.ExperimentRunId,
                        principalSchema: "rbl",
                        principalTable: "ExperimentRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationResult_TestQuestion_TestQuestionId",
                        column: x => x.TestQuestionId,
                        principalSchema: "rbl",
                        principalTable: "TestQuestion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExperimentRunMetric",
                schema: "rbl",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExperimentRunId = table.Column<long>(type: "bigint", nullable: false),
                    AvgFaithfulness = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    AvgAnswerRelevancy = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    AvgContextPrecision = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    AvgContextRecall = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    AvgAnswerCorrectness = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    AvgLatencyMs = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    TotalQuestions = table.Column<int>(type: "int", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentRunMetric", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExperimentRunMetric_ExperimentRun_ExperimentRunId",
                        column: x => x.ExperimentRunId,
                        principalSchema: "rbl",
                        principalTable: "ExperimentRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageCitation",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    ChunkId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RelevanceScore = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    Snippet = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageCitation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageCitation_ChatMessage_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "dbo",
                        principalTable: "ChatMessage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChunkEmbedding",
                schema: "rag",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChunkId = table.Column<long>(type: "bigint", nullable: false),
                    EmbeddingModelId = table.Column<long>(type: "bigint", nullable: false),
                    VectorCollection = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VectorPointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Dimension = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IndexedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkEmbedding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChunkEmbedding_DocumentChunk_ChunkId",
                        column: x => x.ChunkId,
                        principalSchema: "rag",
                        principalTable: "DocumentChunk",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChunkEmbedding_EmbeddingModel_EmbeddingModelId",
                        column: x => x.EmbeddingModelId,
                        principalSchema: "dbo",
                        principalTable: "EmbeddingModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationRetrieval",
                schema: "rbl",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluationResultId = table.Column<long>(type: "bigint", nullable: false),
                    ChunkId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: true),
                    Snippet = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationRetrieval", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationRetrieval_EvaluationResult_EvaluationResultId",
                        column: x => x.EvaluationResultId,
                        principalSchema: "rbl",
                        principalTable: "EvaluationResult",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_AppSetting_Key",
                schema: "dbo",
                table: "AppSetting",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ActorUserId",
                schema: "dbo",
                table: "AuditLog",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CreatedAtUtc",
                schema: "dbo",
                table: "AuditLog",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Target",
                schema: "dbo",
                table: "AuditLog",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TargetUserId",
                schema: "dbo",
                table: "AuditLog",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Chapter_SubjectId",
                schema: "dbo",
                table: "Chapter",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessage_EmbeddingModelId",
                schema: "dbo",
                table: "ChatMessage",
                column: "EmbeddingModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessage_LlmModelId",
                schema: "dbo",
                table: "ChatMessage",
                column: "LlmModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessage_SessionId_CreatedAtUtc",
                schema: "dbo",
                table: "ChatMessage",
                columns: new[] { "SessionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSession_PinnedChunkingStrategyId",
                schema: "dbo",
                table: "ChatSession",
                column: "PinnedChunkingStrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSession_PinnedEmbeddingModelId",
                schema: "dbo",
                table: "ChatSession",
                column: "PinnedEmbeddingModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSession_PinnedLlmModelId",
                schema: "dbo",
                table: "ChatSession",
                column: "PinnedLlmModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSession_SubjectId",
                schema: "dbo",
                table: "ChatSession",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSession_UserId",
                schema: "dbo",
                table: "ChatSession",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbedding_Model",
                schema: "rag",
                table: "ChunkEmbedding",
                column: "EmbeddingModelId")
                .Annotation("SqlServer:Include", new[] { "ChunkId", "VectorCollection", "VectorPointId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_ChunkEmbedding_Chunk_Model",
                schema: "rag",
                table: "ChunkEmbedding",
                columns: new[] { "ChunkId", "EmbeddingModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ChunkEmbedding_PointId",
                schema: "rag",
                table: "ChunkEmbedding",
                columns: new[] { "VectorCollection", "VectorPointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ChunkingStrategy_Name",
                schema: "dbo",
                table: "ChunkingStrategy",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Document_ChapterId",
                schema: "dbo",
                table: "Document",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_Document_Status",
                schema: "dbo",
                table: "Document",
                column: "Status",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Document_SubjectId",
                schema: "dbo",
                table: "Document",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "UQ_Document_Subject_Sha",
                schema: "dbo",
                table: "Document",
                columns: new[] { "SubjectId", "Sha256Checksum" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Chunk_DocumentId",
                schema: "rag",
                table: "DocumentChunk",
                column: "DocumentId")
                .Annotation("SqlServer:Include", new[] { "ChunkingStrategyId", "ChunkIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunk_ChunkingStrategyId",
                schema: "rag",
                table: "DocumentChunk",
                column: "ChunkingStrategyId");

            migrationBuilder.CreateIndex(
                name: "UQ_DocumentChunk_Doc_Strategy_Index",
                schema: "rag",
                table: "DocumentChunk",
                columns: new[] { "DocumentId", "ChunkingStrategyId", "ChunkIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessingJob_ChunkingStrategyId",
                schema: "dbo",
                table: "DocumentProcessingJob",
                column: "ChunkingStrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessingJob_EmbeddingModelId",
                schema: "dbo",
                table: "DocumentProcessingJob",
                column: "EmbeddingModelId");

            migrationBuilder.CreateIndex(
                name: "UQ_DocProcJob_ActivePerDoc",
                schema: "dbo",
                table: "DocumentProcessingJob",
                column: "DocumentId",
                unique: true,
                filter: "[State] IN ('queued','running')");

            migrationBuilder.CreateIndex(
                name: "UQ_EmbeddingModel_Collection",
                schema: "dbo",
                table: "EmbeddingModel",
                column: "QdrantCollectionName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_EmbeddingModel_Name",
                schema: "dbo",
                table: "EmbeddingModel",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResult_RunId",
                schema: "rbl",
                table: "EvaluationResult",
                column: "ExperimentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResult_TestQuestionId",
                schema: "rbl",
                table: "EvaluationResult",
                column: "TestQuestionId");

            migrationBuilder.CreateIndex(
                name: "UQ_EvaluationResult_Run_Question",
                schema: "rbl",
                table: "EvaluationResult",
                columns: new[] { "ExperimentRunId", "TestQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRetrieval_ResultId",
                schema: "rbl",
                table: "EvaluationRetrieval",
                column: "EvaluationResultId");

            migrationBuilder.CreateIndex(
                name: "IX_Experiment_SubjectId",
                schema: "rbl",
                table: "Experiment",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentRun_ChunkingStrategyId",
                schema: "rbl",
                table: "ExperimentRun",
                column: "ChunkingStrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentRun_EmbeddingModelId",
                schema: "rbl",
                table: "ExperimentRun",
                column: "EmbeddingModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentRun_ExperimentId",
                schema: "rbl",
                table: "ExperimentRun",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentRun_LlmModelId",
                schema: "rbl",
                table: "ExperimentRun",
                column: "LlmModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentRun_Status",
                schema: "rbl",
                table: "ExperimentRun",
                column: "Status",
                filter: "[Status] IN ('queued','running')");

            migrationBuilder.CreateIndex(
                name: "UQ_ExperimentRunMetric_RunId",
                schema: "rbl",
                table: "ExperimentRunMetric",
                column: "ExperimentRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Pending",
                schema: "dbo",
                table: "IntegrationOutbox",
                column: "Status",
                filter: "[Status] = 'pending'");

            migrationBuilder.CreateIndex(
                name: "UQ_LlmModel_Name",
                schema: "dbo",
                table: "LlmModel",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageCitation_MessageId",
                schema: "dbo",
                table: "MessageCitation",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetToken_TokenHash",
                schema: "auth",
                table: "PasswordResetToken",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetToken_UserId",
                schema: "auth",
                table: "PasswordResetToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Permission_Code",
                schema: "auth",
                table: "Permission",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_FamilyId",
                schema: "auth",
                table: "RefreshToken",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId",
                schema: "auth",
                table: "RefreshToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_RefreshToken_TokenHash",
                schema: "auth",
                table: "RefreshToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Role_NormalizedName",
                schema: "auth",
                table: "Role",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_PermissionId",
                schema: "auth",
                table: "RolePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UQ_Subject_Code",
                schema: "dbo",
                table: "Subject",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfiguration_ActiveChunkingStrategyId",
                schema: "dbo",
                table: "SystemConfiguration",
                column: "ActiveChunkingStrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfiguration_ActiveEmbeddingModelId",
                schema: "dbo",
                table: "SystemConfiguration",
                column: "ActiveEmbeddingModelId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfiguration_ActiveLlmModelId",
                schema: "dbo",
                table: "SystemConfiguration",
                column: "ActiveLlmModelId");

            migrationBuilder.CreateIndex(
                name: "IX_TestQuestion_SubjectId",
                schema: "rbl",
                table: "TestQuestion",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "UQ_User_NormalizedEmail",
                schema: "auth",
                table: "User",
                column: "NormalizedEmail",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RoleId",
                schema: "auth",
                table: "UserRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubject_SubjectId",
                schema: "auth",
                table: "UserSubject",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSetting",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ChunkEmbedding",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "DocumentProcessingJob",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EvaluationRetrieval",
                schema: "rbl");

            migrationBuilder.DropTable(
                name: "ExperimentRunMetric",
                schema: "rbl");

            migrationBuilder.DropTable(
                name: "IntegrationOutbox",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MessageCitation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PasswordResetToken",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "RefreshToken",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "RolePermission",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "SystemConfiguration",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UserRole",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserSubject",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "DocumentChunk",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "EvaluationResult",
                schema: "rbl");

            migrationBuilder.DropTable(
                name: "ChatMessage",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Permission",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Role",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Document",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ExperimentRun",
                schema: "rbl");

            migrationBuilder.DropTable(
                name: "TestQuestion",
                schema: "rbl");

            migrationBuilder.DropTable(
                name: "ChatSession",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Chapter",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Experiment",
                schema: "rbl");

            migrationBuilder.DropTable(
                name: "ChunkingStrategy",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EmbeddingModel",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LlmModel",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "User",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Subject",
                schema: "dbo");
        }
    }
}
