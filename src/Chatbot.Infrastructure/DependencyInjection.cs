using Chatbot.Application.Common.Interfaces;
using Chatbot.Infrastructure.Identity;
using Chatbot.Infrastructure.Jobs;
using Chatbot.Infrastructure.Ml;
using Chatbot.Infrastructure.Options;
using Chatbot.Infrastructure.Persistence;
using Chatbot.Infrastructure.Persistence.Interceptors;
using Chatbot.Infrastructure.Persistence.Seed;
using Chatbot.Infrastructure.Storage;
using Chatbot.Infrastructure.Time;
using Chatbot.Infrastructure.Vectors;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace Chatbot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName));
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));
        services.AddOptions<PythonMlOptions>().Bind(configuration.GetSection(PythonMlOptions.SectionName));
        services.AddOptions<QdrantOptions>().Bind(configuration.GetSection(QdrantOptions.SectionName));
        services.AddOptions<OllamaOptions>().Bind(configuration.GetSection(OllamaOptions.SectionName));
        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IFileStorageService, DiskFileStorageService>();
        services.AddScoped<IEmailService, Chatbot.Infrastructure.Email.GmailEmailService>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<ChatbotDbContext>((sp, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("SqlServer"),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", Schemas.Dbo));
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ChatbotDbContext>());
        services.AddScoped<DbInitializer>();

        // Qdrant (vector store — .NET owns all vector ops).
        services.AddSingleton(sp =>
        {
            var q = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
            return new QdrantClient(q.Host, q.GrpcPort, q.UseHttps, q.ApiKey);
        });
        services.AddSingleton<IVectorStore, QdrantVectorStore>();

        // Ollama chat generation (Microsoft.Extensions.AI).
        services.AddSingleton<IChatCompletionService, OllamaChatCompletionService>();

        // Python ML service (typed client with X-Internal-Key).
        services.AddHttpClient<IAiServiceClient, PythonMlClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<PythonMlOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
            if (!string.IsNullOrEmpty(o.InternalApiKey))
            {
                client.DefaultRequestHeaders.Add("X-Internal-Key", o.InternalApiKey);
            }
        });

        // Hangfire (background jobs) on SQL Server.
        services.AddHangfire((sp, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                configuration.GetConnectionString("SqlServer"),
                new SqlServerStorageOptions
                {
                    SchemaName = "hangfire",
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = TimeSpan.FromSeconds(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(30),
                    UseRecommendedIsolationLevel = true,
                }));
        services.AddHangfireServer(options =>
        {
            options.Queues = ["ingestion", "evaluation", "finetune", "default"];
        });
        services.AddScoped<IJobScheduler, HangfireJobScheduler>();
        services.AddScoped<IngestDocumentJob>();
        services.AddScoped<RunExperimentJob>();

        return services;
    }
}
