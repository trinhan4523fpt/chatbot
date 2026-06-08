using System.Text.Json;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using Chatbot.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence.Seed;

/// <summary>Applies migrations and idempotently seeds reference data, RBAC, demo subject and admin.</summary>
public sealed class DbInitializer(
    ChatbotDbContext db,
    IPasswordHasher passwordHasher,
    IAiServiceClient ai,
    IOptions<SeedOptions> seedOptions,
    ILogger<DbInitializer> logger)
{
    private readonly SeedOptions _seed = seedOptions.Value;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
        {
            logger.LogInformation("Applying pending EF migrations...");
            await db.Database.MigrateAsync(ct);
        }

        await SeedPermissionsAndRolesAsync(ct);
        await SeedReferenceDataAsync(ct);
        await SeedDemoSubjectAsync(ct);
        await SeedSystemConfigurationAsync(ct);
        await SeedAdminAsync(ct);
        await SeedTestQuestionsAsync(ct);
        await ReconcileEmbeddingDimensionsAsync(ct);

        AssertPermissionMatrix();
        logger.LogInformation("Database initialization complete.");
    }

    private async Task ReconcileEmbeddingDimensionsAsync(CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            var models = (await ai.GetModelsAsync(cts.Token)).ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var em in await db.EmbeddingModels.AsNoTracking().ToListAsync(ct))
            {
                if (models.TryGetValue(em.Name, out var info) && info.Dimension != em.Dimension)
                {
                    logger.LogWarning(
                        "Embedding model {Name} dimension mismatch: DB={Db}, Python={Py}.",
                        em.Name, em.Dimension, info.Dimension);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Embedding dimension reconciliation skipped (Python ML unavailable): {Message}", ex.Message);
        }
    }

    private async Task SeedPermissionsAndRolesAsync(CancellationToken ct)
    {
        var existingPermissions = await db.Permissions.ToDictionaryAsync(p => p.Code, ct);
        foreach (var def in Permissions.All.Where(d => !existingPermissions.ContainsKey(d.Code)))
        {
            db.Permissions.Add(new Permission
            {
                Code = def.Code, Name = def.Name, Category = def.Category, IsSystem = true,
            });
        }

        var existingRoles = await db.Roles.ToDictionaryAsync(r => r.NormalizedName, ct);
        foreach (var roleName in RoleDefinitions.AllRoles)
        {
            if (!existingRoles.ContainsKey(roleName.ToUpperInvariant()))
            {
                db.Roles.Add(new Role
                {
                    Name = roleName, NormalizedName = roleName.ToUpperInvariant(), IsSystem = true,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        // Map role -> permissions (add missing only).
        var perms = await db.Permissions.ToDictionaryAsync(p => p.Code, p => p.Id, ct);
        var roles = await db.Roles.ToDictionaryAsync(r => r.NormalizedName, ct);
        var existingMap = await db.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct);
        var existingMapSet = existingMap.Select(x => (x.RoleId, x.PermissionId)).ToHashSet();

        foreach (var (roleName, codes) in RoleDefinitions.DefaultPermissions)
        {
            var roleId = roles[roleName.ToUpperInvariant()].Id;
            foreach (var code in codes)
            {
                var permId = perms[code];
                if (existingMapSet.Add((roleId, permId)))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permId });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedReferenceDataAsync(CancellationToken ct)
    {
        await EnsureAsync(db.ChunkingStrategies, s => s.Name == "fixed-512-50",
            () => new ChunkingStrategy { Name = "fixed-512-50", ChunkSize = 512, ChunkOverlap = 50, Description = "Fixed-size 512 tokens, 50 overlap" }, ct);
        await EnsureAsync(db.ChunkingStrategies, s => s.Name == "fixed-1024-128",
            () => new ChunkingStrategy { Name = "fixed-1024-128", ChunkSize = 1024, ChunkOverlap = 128, Description = "Fixed-size 1024 tokens, 128 overlap" }, ct);
        await EnsureAsync(db.ChunkingStrategies, s => s.Name == "semantic-paragraph",
            () => new ChunkingStrategy { Name = "semantic-paragraph", Description = "Semantic paragraph splitting" }, ct);

        await EnsureAsync(db.EmbeddingModels, m => m.Name == "multilingual-e5-base",
            () => new EmbeddingModel { Name = "multilingual-e5-base", Provider = "huggingface", Dimension = 768, IsFree = true, MaxInputTokens = 512, QdrantCollectionName = "emb_multilingual_e5_base", Description = "intfloat/multilingual-e5-base" }, ct);
        await EnsureAsync(db.EmbeddingModels, m => m.Name == "phobert-base",
            () => new EmbeddingModel { Name = "phobert-base", Provider = "huggingface", Dimension = 768, IsFree = true, MaxInputTokens = 256, QdrantCollectionName = "emb_phobert_base", Description = "vinai/phobert-base (VN word-seg)" }, ct);
        await EnsureAsync(db.EmbeddingModels, m => m.Name == "bge-m3",
            () => new EmbeddingModel { Name = "bge-m3", Provider = "huggingface", Dimension = 1024, IsFree = true, MaxInputTokens = 8192, QdrantCollectionName = "emb_bge_m3", Description = "BAAI/bge-m3" }, ct);
        await EnsureAsync(db.EmbeddingModels, m => m.Name == "text-embedding-3-small",
            () => new EmbeddingModel { Name = "text-embedding-3-small", Provider = "openai", Dimension = 1536, IsFree = false, MaxInputTokens = 8191, QdrantCollectionName = "emb_openai_3_small", Description = "OpenAI (benchmark only; requires API key)" }, ct);

        await EnsureAsync(db.LlmModels, m => m.Name == "qwen2.5:7b-instruct",
            () => new LlmModel { Name = "qwen2.5:7b-instruct", Type = LlmModelType.Base, Provider = "ollama", BaseModel = "qwen2.5:7b-instruct", Description = "Local Vietnamese-capable base model (RAG generation)" }, ct);
        await EnsureAsync(db.LlmModels, m => m.Name == "chatbot-ft-v1",
            () => new LlmModel { Name = "chatbot-ft-v1", Type = LlmModelType.FineTuned, Provider = "ollama", BaseModel = "qwen2.5:7b-instruct", IsActive = false, Description = "Fine-tuned model placeholder (populated after fine-tuning)" }, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedDemoSubjectAsync(CancellationToken ct)
    {
        if (await db.Subjects.AnyAsync(s => s.Code == "NMCNTT", ct))
        {
            return;
        }

        var subject = new Subject
        {
            Code = "NMCNTT",
            Name = "Nhập môn Công nghệ thông tin",
            Description = "Môn học demo cho hệ thống chatbot RAG.",
            Chapters =
            [
                new Chapter { Title = "Chương 1: Tổng quan CNTT", OrderIndex = 1 },
                new Chapter { Title = "Chương 2: Phần cứng & phần mềm", OrderIndex = 2 },
                new Chapter { Title = "Chương 3: Mạng máy tính", OrderIndex = 3 },
            ],
        };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedSystemConfigurationAsync(CancellationToken ct)
    {
        if (await db.SystemConfigurations.AnyAsync(c => c.Id == 1, ct))
        {
            return;
        }

        var embedding = await db.EmbeddingModels.FirstAsync(m => m.Name == "multilingual-e5-base", ct);
        var strategy = await db.ChunkingStrategies.FirstAsync(s => s.Name == "fixed-512-50", ct);
        var llm = await db.LlmModels.FirstAsync(m => m.Name == "qwen2.5:7b-instruct", ct);

        db.SystemConfigurations.Add(new SystemConfiguration
        {
            Id = 1,
            ActiveEmbeddingModelId = embedding.Id,
            ActiveChunkingStrategyId = strategy.Id,
            ActiveLlmModelId = llm.Id,
            PromptTemplate = DefaultPromptTemplate,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        var normalizedEmail = _seed.Admin.Email.ToUpperInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_seed.Admin.Password))
        {
            logger.LogWarning("Seed:Admin:Password not set — skipping admin user creation. Set it via secrets/env.");
            return;
        }

        var adminRole = await db.Roles.FirstAsync(r => r.NormalizedName == RoleDefinitions.Admin.ToUpperInvariant(), ct);
        var admin = new User
        {
            FullName = _seed.Admin.FullName,
            Email = _seed.Admin.Email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHasher.Hash(_seed.Admin.Password),
            IsActive = true,
            MustChangePassword = true,
            EmailConfirmed = true,
            UserRoles = [new UserRole { RoleId = adminRole.Id }],
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded admin user {Email} (must change password on first login).", _seed.Admin.Email);
    }

    private async Task SeedTestQuestionsAsync(CancellationToken ct)
    {
        var path = FindSeedFile("test-questions.json");
        if (path is null)
        {
            logger.LogInformation("Test-question seed file not found; skipping (import via API later).");
            return;
        }

        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Code == "NMCNTT", ct);
        if (subject is null)
        {
            return;
        }

        var json = await File.ReadAllTextAsync(path, ct);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };
        var seeds = JsonSerializer.Deserialize<List<TestQuestionSeed>>(json, options) ?? [];

        var existing = (await db.TestQuestions
                .Where(q => q.SubjectId == subject.Id && q.ExternalRef != null)
                .Select(q => q.ExternalRef!)
                .ToListAsync(ct))
            .ToHashSet();

        var added = 0;
        foreach (var seed in seeds.Where(s => s.ExternalRef is null || !existing.Contains(s.ExternalRef)))
        {
            Domain.Enums.Difficulty? difficulty =
                Enum.TryParse<Domain.Enums.Difficulty>(seed.Difficulty, ignoreCase: true, out var d) ? d : null;
            db.TestQuestions.Add(new TestQuestion
            {
                SubjectId = subject.Id,
                Question = seed.Question,
                GroundTruth = seed.GroundTruth,
                ReferenceContext = seed.ReferenceContext,
                Difficulty = difficulty,
                ExternalRef = seed.ExternalRef,
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} test questions for subject {Subject}.", added, subject.Code);
        }
    }

    private static string? FindSeedFile(string fileName)
    {
        var candidates = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), "data", "seed", fileName),
            Path.Combine(AppContext.BaseDirectory, "data", "seed", fileName),
        };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            candidates.Add(Path.Combine(dir.FullName, "data", "seed", fileName));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private sealed record TestQuestionSeed(
        string Question, string GroundTruth, string? ReferenceContext, string? Difficulty, string? ExternalRef);

    private void AssertPermissionMatrix()
    {
        var defined = Permissions.All.Select(p => p.Code).ToHashSet();
        var referenced = RoleDefinitions.DefaultPermissions.Values.SelectMany(x => x).ToHashSet();
        var missing = referenced.Except(defined).ToList();
        if (missing.Count != 0)
        {
            throw new InvalidOperationException(
                $"Role map references undefined permissions: {string.Join(", ", missing)}");
        }
    }

    private static async Task EnsureAsync<T>(
        DbSet<T> set, System.Linq.Expressions.Expression<Func<T, bool>> predicate, Func<T> factory, CancellationToken ct)
        where T : class
    {
        if (!await set.AnyAsync(predicate, ct))
        {
            set.Add(factory());
        }
    }

    private const string DefaultPromptTemplate =
        """
        Bạn là trợ lý học tập. Chỉ trả lời dựa trên [NỘI DUNG THAM KHẢO] bên dưới.
        Nếu thông tin không có trong tài liệu, hãy trả lời đúng câu: "Tôi không tìm thấy thông tin này trong tài liệu."
        Trả lời bằng tiếng Việt, ngắn gọn, và trích dẫn nguồn dạng [Nguồn i].

        [NỘI DUNG THAM KHẢO]
        {context}

        [CÂU HỎI]
        {question}
        """;
}
