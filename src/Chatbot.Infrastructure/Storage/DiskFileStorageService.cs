using System.Security.Cryptography;
using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Storage;

public sealed class DiskFileStorageService : IFileStorageService
{
    private const string TmpDir = "tmp";
    private const string DocumentsDir = "documents";
    private readonly string _root;

    public DiskFileStorageService(IOptions<StorageOptions> options)
    {
        var root = options.Value.Root;
        _root = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(Directory.GetCurrentDirectory(), root));
        Directory.CreateDirectory(Path.Combine(_root, TmpDir));
        Directory.CreateDirectory(Path.Combine(_root, DocumentsDir));
    }

    public async Task<StagedFile> StageAsync(Stream content, CancellationToken ct = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var tmpPath = Path.Combine(_root, TmpDir, token);

        byte[] header = [];
        long total = 0;
        using var sha = SHA256.Create();
        await using (var fs = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                sha.TransformBlock(buffer, 0, read, null, 0);
                await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                if (header.Length == 0)
                {
                    header = buffer.AsSpan(0, Math.Min(read, 16)).ToArray();
                }

                total += read;
            }

            sha.TransformFinalBlock([], 0, 0);
        }

        return new StagedFile(token, Convert.ToHexStringLower(sha.Hash!), total, header);
    }

    public Task<string> CommitAsync(
        string token, long subjectId, long documentId, string storedFileName, CancellationToken ct = default)
    {
        var tmpPath = Path.Combine(_root, TmpDir, token);
        if (!File.Exists(tmpPath))
        {
            throw new NotFoundException("Tệp tạm không tồn tại hoặc đã hết hạn.");
        }

        var relativeDir = $"{DocumentsDir}/{subjectId}/{documentId}";
        Directory.CreateDirectory(Path.Combine(_root, relativeDir));
        var relativePath = $"{relativeDir}/{storedFileName}";
        File.Move(tmpPath, Path.Combine(_root, relativePath), overwrite: true);
        return Task.FromResult(relativePath);
    }

    public Task DiscardAsync(string token, CancellationToken ct = default)
    {
        var tmpPath = Path.Combine(_root, TmpDir, token);
        if (File.Exists(tmpPath))
        {
            File.Delete(tmpPath);
        }

        return Task.CompletedTask;
    }

    public Task DeleteDocumentDirectoryAsync(long subjectId, long documentId, CancellationToken ct = default)
    {
        var dir = Path.Combine(_root, DocumentsDir, subjectId.ToString(), documentId.ToString());
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        return Task.CompletedTask;
    }

    public string ResolvePhysicalPath(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Đường dẫn tệp không hợp lệ.");
        }

        return full;
    }
}
