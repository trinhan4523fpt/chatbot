using System.Text;

namespace Chatbot.Application.Common;

/// <summary>Guards that generated answers stay in Vietnamese.</summary>
public static class AnswerLanguagePolicy
{
    /// <summary>
    /// True if the text contains Chinese characters. The local qwen2.5 model sometimes drifts into
    /// Chinese mid-answer despite the Vietnamese-only instruction. Vietnamese is written in Latin
    /// script, so any of these means the answer is invalid.
    /// </summary>
    public static bool ContainsChinese(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (IsChinese(text, i, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes Chinese characters, and any now-orphaned whitespace, as a last resort when the model
    /// will not stop producing them. The result can read awkwardly, but it never shows the user
    /// Chinese. Prefer regenerating the answer; only strip when that has already failed.
    /// </summary>
    public static string StripChinese(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (IsChinese(text, i, out var charLength))
            {
                i += charLength - 1;
                continue;
            }

            sb.Append(text[i]);
        }

        // Collapse the gaps left behind, e.g. "khoa học 史 lịch sử" -> "khoa học lịch sử".
        var parts = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts).Trim();
    }

    /// <summary>
    /// Whether the character at <paramref name="index"/> is Chinese, and how many UTF-16 code units
    /// it spans (2 for the astral-plane extensions, which are encoded as surrogate pairs).
    /// </summary>
    private static bool IsChinese(string text, int index, out int charLength)
    {
        charLength = 1;
        var c = text[index];

        // Astral-plane ideographs (Extensions B-G, U+20000-U+3FFFF) arrive as surrogate pairs.
        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            charLength = 2;
            var codePoint = char.ConvertToUtf32(c, text[index + 1]);
            return codePoint is >= 0x20000 and <= 0x3FFFF;
        }

        return c switch
        {
            >= '⺀' and <= '⻿' => true, // CJK radicals supplement
            >= '　' and <= '〿' => true, // CJK punctuation: 、。〈〉
            >= '㈀' and <= '䶿' => true, // enclosed CJK letters + Extension A
            >= '一' and <= '鿿' => true, // CJK Unified Ideographs (the common case)
            >= '豈' and <= '﫿' => true, // compatibility ideographs: 豈更車
            >= '︰' and <= '﹏' => true, // compatibility forms
            >= '＀' and <= '￯' => true, // fullwidth forms: ，！（）
            _ => false,
        };
    }
}
