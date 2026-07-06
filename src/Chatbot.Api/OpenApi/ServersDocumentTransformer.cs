using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Chatbot.Api.OpenApi;

/// <summary>
/// Populates the OpenAPI document's `servers` list from configuration (OpenApi:Servers), so Scalar
/// shows a server dropdown (e.g. localhost + a dev-tunnel URL) to choose the base URL when calling APIs.
/// </summary>
public sealed class ServersDocumentTransformer(IConfiguration configuration) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var servers = configuration.GetSection("OpenApi:Servers").Get<ServerEntry[]>();
        if (servers is { Length: > 0 })
        {
            document.Servers = servers
                .Where(s => !string.IsNullOrWhiteSpace(s.Url))
                .Select(s => new OpenApiServer { Url = s.Url, Description = s.Description })
                .ToList();
        }

        return Task.CompletedTask;
    }

    private sealed class ServerEntry
    {
        public string Url { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
