using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SwitchChatModelToGemma2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seeding only registers models when the database is first created, so existing
            // databases keep pointing at qwen2.5 -- which drifted from Vietnamese into Chinese.
            // Register gemma2 (chat) and llama3.1 (benchmark judge), then repoint the active
            // config. Guarded throughout so a re-run, or an operator who already chose another
            // model, is left alone.
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM dbo.LlmModel WHERE Name = N'gemma2:9b')
                INSERT INTO dbo.LlmModel (Name, Type, Provider, BaseModel, IsActive, Description, CreatedAtUtc)
                VALUES (N'gemma2:9b', N'base', N'ollama', N'gemma2:9b', 1,
                        N'Local Vietnamese-capable base model (RAG generation)', SYSUTCDATETIME());
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM dbo.LlmModel WHERE Name = N'llama3.1:8b')
                INSERT INTO dbo.LlmModel (Name, Type, Provider, BaseModel, IsActive, Description, CreatedAtUtc)
                VALUES (N'llama3.1:8b', N'base', N'ollama', N'llama3.1:8b', 0,
                        N'Benchmark judge (emits JSON scores, not user-facing prose)', SYSUTCDATETIME());
                """);

            // Retire qwen2.5, but keep the row: existing ChatMessage rows reference it.
            migrationBuilder.Sql(
                """
                UPDATE dbo.LlmModel
                SET IsActive = 0,
                    Description = N'Superseded by gemma2: drifted from Vietnamese into Chinese'
                WHERE Name = N'qwen2.5:7b-instruct';
                """);

            migrationBuilder.Sql(
                """
                UPDATE c
                SET c.ActiveLlmModelId = g.Id
                FROM dbo.SystemConfiguration c
                JOIN dbo.LlmModel g ON g.Name = N'gemma2:9b'
                JOIN dbo.LlmModel q ON q.Id = c.ActiveLlmModelId
                WHERE c.Id = 1 AND q.Name = N'qwen2.5:7b-instruct';
                """);

            // Sessions pinned to qwen2.5 would otherwise keep generating Chinese.
            migrationBuilder.Sql(
                """
                UPDATE s
                SET s.PinnedLlmModelId = g.Id
                FROM dbo.ChatSession s
                JOIN dbo.LlmModel g ON g.Name = N'gemma2:9b'
                JOIN dbo.LlmModel q ON q.Id = s.PinnedLlmModelId
                WHERE q.Name = N'qwen2.5:7b-instruct';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
