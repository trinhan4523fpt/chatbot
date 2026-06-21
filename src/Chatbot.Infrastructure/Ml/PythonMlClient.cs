using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;

namespace Chatbot.Infrastructure.Ml;

public sealed class PythonMlClient(HttpClient http) : IAiServiceClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<ParseResultDto> ParseAsync(byte[] content, string fileName, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);

        using var response = await http.PostAsync("/parse", form, ct);
        await EnsureSuccessAsync(response, "/parse", ct);
        return (await response.Content.ReadFromJsonAsync<ParseResultDto>(Json, ct))!;
    }

    public async Task<IReadOnlyList<ChunkDto>> ChunkAsync(
        IReadOnlyList<ParsedPageDto> pages, string strategy, int chunkSize, int chunkOverlap, CancellationToken ct = default)
    {
        var payload = new { pages, strategy, chunkSize, chunkOverlap };
        using var response = await http.PostAsJsonAsync("/chunk", payload, Json, ct);
        await EnsureSuccessAsync(response, "/chunk", ct);
        var result = (await response.Content.ReadFromJsonAsync<ChunkEnvelope>(Json, ct))!;
        return result.Chunks;
    }

    public async Task<EmbedResultDto> EmbedAsync(
        IReadOnlyList<string> texts, string model, string inputType, CancellationToken ct = default)
    {
        var payload = new { texts, model, inputType };
        using var response = await http.PostAsJsonAsync("/embed", payload, Json, ct);
        await EnsureSuccessAsync(response, "/embed", ct);
        return (await response.Content.ReadFromJsonAsync<EmbedResultDto>(Json, ct))!;
    }

    public async Task<IReadOnlyList<EmbeddingModelInfoDto>> GetModelsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("/models", ct);
        await EnsureSuccessAsync(response, "/models", ct);
        var result = (await response.Content.ReadFromJsonAsync<ModelsEnvelope>(Json, ct))!;
        return result.Models;
    }

    public async Task<RagEvalResult> RagEvalAsync(
        IReadOnlyList<RagEvalItem> items, string judgeModel, CancellationToken ct = default)
    {
        var payload = new
        {
            Items = items.Select(i => new
            {
                i.Question, i.Answer, i.Contexts, i.GroundTruth, i.ReferenceContext,
            }),
            JudgeModel = judgeModel,
        };
        using var response = await http.PostAsJsonAsync("/rag-eval", payload, Json, ct);
        await EnsureSuccessAsync(response, "/rag-eval", ct);
        var result = (await response.Content.ReadFromJsonAsync<RagEvalEnvelope>(Json, ct))!;
        return new RagEvalResult(result.PerItem);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string endpoint, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new AiServiceException((int)response.StatusCode, $"Python ML {endpoint} failed ({(int)response.StatusCode}): {body}");
    }

    private sealed record ChunkEnvelope(IReadOnlyList<ChunkDto> Chunks);

    private sealed record ModelsEnvelope(IReadOnlyList<EmbeddingModelInfoDto> Models);

    private sealed record RagEvalEnvelope(IReadOnlyList<RagEvalResultItem> PerItem);
}
