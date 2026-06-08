using Chatbot.Domain.Enums;

namespace Chatbot.Application.Common.Files;

/// <summary>Allow-list of uploadable document types, validated by extension + magic bytes.</summary>
public static class FileTypePolicy
{
    private sealed record AllowedType(string Extension, FileType FileType, byte[][] MagicNumbers);

    private static readonly AllowedType[] Allowed =
    [
        new(".pdf", FileType.Pdf, [[0x25, 0x50, 0x44, 0x46]]),                       // %PDF
        new(".docx", FileType.Docx, [[0x50, 0x4B, 0x03, 0x04]]),                     // ZIP (OOXML)
        new(".pptx", FileType.Slide, [[0x50, 0x4B, 0x03, 0x04]]),                    // ZIP (OOXML)
        new(".ppt", FileType.Slide, [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]]), // legacy OLE
    ];

    public static bool TryValidate(
        string fileName, ReadOnlySpan<byte> header, out FileType fileType, out string extension, out string error)
    {
        fileType = default;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        extension = ext;
        error = string.Empty;

        AllowedType? allowed = null;
        foreach (var candidate in Allowed)
        {
            if (candidate.Extension == ext)
            {
                allowed = candidate;
                break;
            }
        }

        if (allowed is null)
        {
            error = $"Định dạng tệp '{ext}' không được hỗ trợ. Chỉ chấp nhận PDF, DOCX, PPTX/PPT.";
            return false;
        }

        var matchesMagic = false;
        foreach (var magic in allowed.MagicNumbers)
        {
            if (StartsWith(header, magic))
            {
                matchesMagic = true;
                break;
            }
        }

        if (!matchesMagic)
        {
            error = "Nội dung tệp không khớp với phần mở rộng (magic bytes không hợp lệ).";
            return false;
        }

        fileType = allowed.FileType;
        return true;
    }

    private static bool StartsWith(ReadOnlySpan<byte> header, byte[] prefix)
    {
        if (header.Length < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (header[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }
}
