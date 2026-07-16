namespace Chatbot.Application.Common;

/// <summary>Guards that generated answers stay in Vietnamese.</summary>
public static class AnswerLanguagePolicy
{
    /// <summary>
    /// True if the text contains CJK ideographs. The local qwen2.5 model sometimes drifts into
    /// Chinese mid-answer despite the Vietnamese-only instruction; Vietnamese is written in Latin
    /// script, so any ideograph means the answer is invalid.
    /// </summary>
    public static bool ContainsChinese(string text)
    {
        foreach (var c in text)
        {
            // CJK Unified Ideographs + Extension A. Vietnamese diacritics live in Latin ranges.
            if (c is >= '一' and <= '鿿' or >= '㐀' and <= '䶿')
            {
                return true;
            }
        }

        return false;
    }
}
