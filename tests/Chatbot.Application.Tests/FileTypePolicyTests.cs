using Chatbot.Application.Common.Files;
using Chatbot.Domain.Enums;

namespace Chatbot.Application.Tests;

public class FileTypePolicyTests
{
    private static readonly byte[] PdfHeader = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];
    private static readonly byte[] ZipHeader = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];

    [Fact]
    public void ValidPdf_Passes()
    {
        var ok = FileTypePolicy.TryValidate("bai-giang.pdf", PdfHeader, out var type, out var ext, out _);
        Assert.True(ok);
        Assert.Equal(FileType.Pdf, type);
        Assert.Equal(".pdf", ext);
    }

    [Fact]
    public void Docx_WithZipMagic_Passes_AsSlideOrDocx()
    {
        var ok = FileTypePolicy.TryValidate("notes.docx", ZipHeader, out var type, out _, out _);
        Assert.True(ok);
        Assert.Equal(FileType.Docx, type);
    }

    [Fact]
    public void Pptx_MapsToSlide()
    {
        var ok = FileTypePolicy.TryValidate("slides.pptx", ZipHeader, out var type, out _, out _);
        Assert.True(ok);
        Assert.Equal(FileType.Slide, type);
    }

    [Fact]
    public void WrongMagic_Fails()
    {
        var ok = FileTypePolicy.TryValidate("fake.pdf", ZipHeader, out _, out _, out var error);
        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void UnsupportedExtension_Fails()
    {
        var ok = FileTypePolicy.TryValidate("malware.exe", [0x4D, 0x5A], out _, out _, out var error);
        Assert.False(ok);
        Assert.NotEmpty(error);
    }
}
