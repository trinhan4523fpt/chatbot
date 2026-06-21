namespace Chatbot.Api.Contracts;

public sealed record CreateSubjectRequest(string Code, string Name, string? Description);

public sealed record UpdateSubjectRequest(string Name, string? Description);

public sealed record CreateChapterRequest(string Title, int OrderIndex);

public sealed record AssignInstructorRequest(long UserId);

public sealed class UploadDocumentForm
{
    public IFormFile File { get; set; } = null!;
    public long SubjectId { get; set; }
    public long? ChapterId { get; set; }
    public string? Title { get; set; }
}
