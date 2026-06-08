namespace Chatbot.Infrastructure.Options;

public sealed class PythonMlOptions
{
    public const string SectionName = "PythonMl";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string InternalApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
}

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string Host { get; set; } = "localhost";
    public int GrpcPort { get; set; } = 6334;
    public bool UseHttps { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "qwen2.5:7b-instruct";
    public string JudgeModel { get; set; } = "qwen2.5:7b-instruct";
}
