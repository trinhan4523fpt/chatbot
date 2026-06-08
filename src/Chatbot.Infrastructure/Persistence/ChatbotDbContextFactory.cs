using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chatbot.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef` can build the model without booting the API.
/// Reads the connection string from ConnectionStrings__SqlServer (falls back to the dev default).
/// </summary>
public sealed class ChatbotDbContextFactory : IDesignTimeDbContextFactory<ChatbotDbContext>
{
    public ChatbotDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer")
            ?? "Server=localhost,1433;Database=ChatbotDb;User Id=sa;Password=Chatbot_Dev_P@ssw0rd1;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<ChatbotDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", Schemas.Dbo))
            .Options;

        return new ChatbotDbContext(options);
    }
}
