using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Brain.Application;

namespace Brain.Infrastructure;

/// <summary>Offline test/development feature hashing, not a learned semantic model.</summary>
public sealed partial class DevelopmentEmbeddingProvider : IEmbeddingProvider
{
    public EmbeddingModel Model { get; } = new("development", "token-hash-v1", 384);
    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vector = new float[Model.Dimensions];
        var tokens = Tokens().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToArray();
        // Ensure punctuation-only text still has a well-defined nonzero vector.
        if (tokens.Length == 0) tokens = [text];
        foreach (var token in tokens)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = BinaryPrimitives.ReadUInt32LittleEndian(hash) % (uint)vector.Length;
            vector[index] += 1;
        }
        var norm = Math.Sqrt(vector.Sum(v => (double)v * v));
        for (var i = 0; i < vector.Length; i++) vector[i] /= (float)norm;
        return Task.FromResult(vector);
    }
    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Tokens();
}
