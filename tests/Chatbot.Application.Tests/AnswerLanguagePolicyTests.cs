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

    [Fact]
    public void TrailingChineseTail_IsDetected()
    {
        // Real leak (message 10023): a fluent Vietnamese answer that ended in Chinese.
        const string answer =
            "Chức năng dự báo và phê phán của môn học Lịch sử Đảng Cộng sản Việt Nam có ý nghĩa " +
            "quan trọng: rút ra bài học kinh nghiệm để khắc phục hạn chế và cải tiến公作改进。";
        Assert.True(AnswerLanguagePolicy.ContainsChinese(answer));
    }

    [Theory]
    [InlineData("Kết luận：đúng", "fullwidth colon")]
    [InlineData("Đảng、Nhà nước", "CJK punctuation")]
    [InlineData("Tài liệu 豈 nói", "compatibility ideograph")]
    [InlineData("Chữ hiếm \U00020000 ở đây", "astral-plane ideograph")]
    public void CharactersOutsideTheCommonBlock_AreDetected(string answer, string why)
    {
        Assert.True(AnswerLanguagePolicy.ContainsChinese(answer), why);
    }

    [Fact]
    public void StripChinese_RemovesCharactersAndTidiesGaps()
    {
        Assert.Equal(
            "khoa học lịch sử",
            AnswerLanguagePolicy.StripChinese("khoa học 史 lịch sử"));
    }

    [Fact]
    public void StripChinese_LeavesCleanVietnameseUntouched()
    {
        const string answer = "Bộ môn Lịch sử Đảng thuộc lĩnh vực khoa học lịch sử. [Nguồn 1]";
        Assert.Equal(answer, AnswerLanguagePolicy.StripChinese(answer));
    }

    [Fact]
    public void StripChinese_OutputNeverContainsChinese()
    {
        const string answer = "Bộ môn Lịch sử Đảng là một chuyên ngành, thuộc khoa học lịch史记，部门属于历史科学领域。[来源 1]";
        var stripped = AnswerLanguagePolicy.StripChinese(answer);
        Assert.False(AnswerLanguagePolicy.ContainsChinese(stripped));
        Assert.Contains("Bộ môn Lịch sử Đảng", stripped);
    }
}
