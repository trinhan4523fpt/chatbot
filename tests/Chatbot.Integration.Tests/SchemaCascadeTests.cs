using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using Chatbot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Chatbot.Integration.Tests;

/// <summary>
/// Applies the EF migration to a real SQL Server 2022 container (surfaces any multiple-cascade-path
/// error) and exercises the document/chunk/embedding cascade chain at runtime.
/// </summary>
public sealed class SchemaCascadeTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private ChatbotDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var options = new DbContextOptionsBuilder<ChatbotDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
        _db = new ChatbotDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Migration_Applies_AndCascadesWork()
    {
        var strategy = new ChunkingStrategy { Name = "fixed", ChunkSize = 512, ChunkOverlap = 50 };
        var model = new EmbeddingModel { Name = "e5", Dimension = 768, QdrantCollectionName = "emb_e5" };
        var subject = new Subject { Code = "C1", Name = "Test" };
        _db.AddRange(strategy, model, subject);
        await _db.SaveChangesAsync();

        var document = new Document
        {
            SubjectId = subject.Id, Title = "Tài liệu", OriginalFileName = "a.pdf", StoredFileName = "a.pdf",
            ContentType = "application/pdf", FileType = FileType.Pdf, FileExtension = ".pdf",
            RelativePath = "documents/1/1/a.pdf", Sha256Checksum = new string('a', 64), SizeBytes = 10,
        };
        _db.Add(document);
        await _db.SaveChangesAsync();

        var chunk = new DocumentChunk
        {
            DocumentId = document.Id, ChunkingStrategyId = strategy.Id, ChunkIndex = 0, Content = "x",
        };
        _db.Add(chunk);
        await _db.SaveChangesAsync();

        _db.Add(new ChunkEmbedding
        {
            ChunkId = chunk.Id, EmbeddingModelId = model.Id, VectorCollection = "emb_e5",
            VectorPointId = Guid.NewGuid(), Dimension = 768, Status = ChunkEmbeddingStatus.Indexed,
        });
        await _db.SaveChangesAsync();

        // Cascade 1: deleting a chunk cascades to its embeddings.
        _db.DocumentChunks.Remove(chunk);
        await _db.SaveChangesAsync();
        Assert.Equal(0, await _db.ChunkEmbeddings.CountAsync());

        // Cascade 2: hard-deleting a document cascades to its chunks (ExecuteDelete bypasses soft-delete).
        _db.Add(new DocumentChunk { DocumentId = document.Id, ChunkingStrategyId = strategy.Id, ChunkIndex = 1, Content = "y" });
        await _db.SaveChangesAsync();
        await _db.Documents.IgnoreQueryFilters().Where(d => d.Id == document.Id).ExecuteDeleteAsync();
        Assert.Equal(0, await _db.DocumentChunks.CountAsync());
    }
}
