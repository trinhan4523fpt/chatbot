using Chatbot.Api;
using Chatbot.Api.Authentication;
using Chatbot.Api.Infrastructure;
using Chatbot.Application;
using Chatbot.Infrastructure;
using Chatbot.Infrastructure.Persistence.Seed;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

// Bootstrap logger captures failures that happen before the host is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddWebApi(builder.Configuration);
    builder.Services.AddOpenApi(options =>
        options.AddDocumentTransformer<Chatbot.Api.OpenApi.ServersDocumentTransformer>());

    // --- Health checks: three tiers -------------------------------------------------
    var sqlConnectionString = builder.Configuration.GetConnectionString("SqlServer");
    var qdrantRestUrl = builder.Configuration["Qdrant:RestUrl"];
    var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"];
    var pythonMlBaseUrl = builder.Configuration["PythonMl:BaseUrl"];

    var healthChecks = builder.Services.AddHealthChecks();
    healthChecks.AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

    if (!string.IsNullOrWhiteSpace(sqlConnectionString))
    {
        healthChecks.AddSqlServer(sqlConnectionString, name: "sqlserver", tags: ["ready"]);
    }

    if (!string.IsNullOrWhiteSpace(qdrantRestUrl))
    {
        healthChecks.AddUrlGroup(new Uri($"{qdrantRestUrl.TrimEnd('/')}/readyz"),
            name: "qdrant", failureStatus: HealthStatus.Degraded, tags: ["deps"]);
    }

    if (!string.IsNullOrWhiteSpace(ollamaBaseUrl))
    {
        healthChecks.AddUrlGroup(new Uri($"{ollamaBaseUrl.TrimEnd('/')}/"),
            name: "ollama", failureStatus: HealthStatus.Degraded, tags: ["deps"]);
    }

    if (!string.IsNullOrWhiteSpace(pythonMlBaseUrl))
    {
        healthChecks.AddUrlGroup(new Uri($"{pythonMlBaseUrl.TrimEnd('/')}/health"),
            name: "python-ml", failureStatus: HealthStatus.Degraded, tags: ["deps"]);
    }

    var app = builder.Build();

    // Apply migrations + seed reference data, RBAC and admin at startup.
    using (var scope = app.Services.CreateScope())
    {
        var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
        await initializer.InitializeAsync();
    }

    app.UseExceptionHandler();

    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        await next();
    });

    app.UseSerilogRequestLogging();
    app.UseCors();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<PasswordChangeGateMiddleware>();

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthFilter(app.Environment.IsDevelopment())],
    });

    app.MapControllers();
    app.MapHub<Chatbot.Api.Hubs.ChatHub>("/hubs/chat");

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync,
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync,
    });
    app.MapHealthChecks("/health/deps", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("deps"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync,
    });

    Log.Information("Chatbot API starting up");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Chatbot API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposed for WebApplicationFactory in integration/API tests.
public partial class Program;
