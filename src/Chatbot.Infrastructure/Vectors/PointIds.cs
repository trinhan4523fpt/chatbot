using System.Security.Cryptography;
using System.Text;

namespace Chatbot.Infrastructure.Vectors;

/// <summary>Deterministic Qdrant point ids (name-based UUIDv5) so re-ingesting a chunk overwrites its point.</summary>
public static class PointIds
{
    private static readonly Guid Namespace = new("7d8a1f2c-9b3e-4c6a-8f10-2a5d6e7c9b04");

    public static Guid For(long chunkId, long embeddingModelId)
    {
        var name = Encoding.UTF8.GetBytes($"{chunkId}:{embeddingModelId}");
        var ns = Namespace.ToByteArray();
        var input = new byte[ns.Length + name.Length];
        Buffer.BlockCopy(ns, 0, input, 0, ns.Length);
        Buffer.BlockCopy(name, 0, input, ns.Length, name.Length);

        var hash = SHA1.HashData(input);
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50); // version 5
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(guidBytes);
    }
}
