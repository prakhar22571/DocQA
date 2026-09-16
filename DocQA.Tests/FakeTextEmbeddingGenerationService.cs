using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace DocQA.Tests;

#pragma warning disable CS0618 // Stubbing the same (obsolete) ITextEmbeddingGenerationService the app code depends on.

/// <summary>
/// Deterministic bag-of-words embedding stub, so tests exercise real cosine-similarity search
/// behavior without calling the OpenAI embedding API.
/// </summary>
public class FakeTextEmbeddingGenerationService : ITextEmbeddingGenerationService
{
    private const int Dimensions = 2048; // matches DocumentChunk.Embedding's [VectorStoreVector(2048)]

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        IList<ReadOnlyMemory<float>> embeddings = data.Select(Embed).ToList();
        return Task.FromResult(embeddings);
    }

    private static ReadOnlyMemory<float> Embed(string text)
    {
        var vector = new float[Dimensions];
        var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var word in words)
        {
            var bucket = (int)((uint)word.GetHashCode() % Dimensions);
            vector[bucket] += 1f;
        }

        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return vector;
    }
}
