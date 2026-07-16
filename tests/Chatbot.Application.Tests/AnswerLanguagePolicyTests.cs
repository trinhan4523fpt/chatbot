using Chatbot.Application.Common;

namespace Chatbot.Application.Tests;

public class AnswerLanguagePolicyTests
{
    [Fact]
    public void AnswerDriftingIntoChinese_IsDetected()
    {
        // Real regression: qwen2.5 answered in Vietnamese then drifted into Chinese mid-sentence.
        const string answer =
            "Bộ môn Lịch sử Đảng Cộng sản Việt Nam là một chuyên ngành, thuộc lĩnh vực khoa học lịch史记";
        Assert.True(AnswerLanguagePolicy.ContainsChinese(answer));
    }

    [Fact]
    public void FullyChineseAnswer_IsDetected()
    {
        Assert.True(AnswerLanguagePolicy.ContainsChinese("根据提供的内容参考资料，回答如下"));
    }

    [Fact]
    public void VietnameseAnswer_WithAllDiacritics_IsNotFlagged()
    {
        const string answer =
            "Bộ môn Lịch sử Đảng Cộng sản Việt Nam là một chuyên ngành thuộc lĩnh vực " +
            "khoa học lịch sử. Đại hội III (1960), ngày 13/7/1992 [Nguồn 1].";
        Assert.False(AnswerLanguagePolicy.ContainsChinese(answer));
    }

    [Fact]
    public void ScopeRestrictedMessage_IsNotFlagged()
    {
        Assert.False(AnswerLanguagePolicy.ContainsChinese("Tôi không tìm thấy thông tin này trong tài liệu."));
    }

    [Fact]
    public void EmptyAnswer_IsNotFlagged()
    {
        Assert.False(AnswerLanguagePolicy.ContainsChinese(string.Empty));
    }
}
