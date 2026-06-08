using Chatbot.Domain.Enums;
using Chatbot.Infrastructure.Persistence.Conventions;
using Chatbot.Infrastructure.Vectors;

namespace Chatbot.Integration.Tests;

public class ConverterTests
{
    [Fact]
    public void EnumTokens_ToToken_ProducesSnakeCase()
    {
        Assert.Equal("rag_vs_finetune", EnumTokens.ToToken(ExperimentType.RagVsFinetune));
        Assert.Equal("fine_tuned", EnumTokens.ToToken(LlmModelType.FineTuned));
        Assert.Equal("pdf", EnumTokens.ToToken(FileType.Pdf));
    }

    [Fact]
    public void EnumTokens_FromToken_ParsesSnakeCase()
    {
        Assert.Equal(ExperimentType.RagVsFinetune, EnumTokens.FromToken<ExperimentType>("rag_vs_finetune"));
        Assert.Equal(LlmModelType.FineTuned, EnumTokens.FromToken<LlmModelType>("fine_tuned"));
        Assert.Equal(DocumentStatus.Indexed, EnumTokens.FromToken<DocumentStatus>("indexed"));
    }

    [Fact]
    public void PointIds_AreDeterministic_AndDifferPerModel()
    {
        var a1 = PointIds.For(42, 1);
        var a2 = PointIds.For(42, 1);
        var b = PointIds.For(42, 2);
        Assert.Equal(a1, a2);
        Assert.NotEqual(a1, b);
        Assert.NotEqual(Guid.Empty, a1);
    }
}
