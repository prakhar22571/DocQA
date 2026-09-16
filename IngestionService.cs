using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace DocQA;

public class IngestionService(Kernel kernel, VectorStoreCollection<string, DocumentChunk> collection)
{
    private const int TargetChunkSize = 500;

    public async Task IngestAsync(string sourceDocument, string rawText)
    {
        var chunks = ChunkText(rawText);

#pragma warning disable CS0618 // ITextEmbeddingGenerationService is obsolete in favor of IEmbeddingGenerator, but it's what AddOpenAITextEmbeddingGeneration registers on the kernel.
        var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        var embeddings = await embeddingService.GenerateEmbeddingsAsync(chunks);
#pragma warning restore CS0618

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = new DocumentChunk
            {
                Id = Guid.NewGuid().ToString(),
                SourceDocument = sourceDocument,
                Content = chunks[i],
                Embedding = embeddings[i]
            };

            await collection.UpsertAsync(chunk);
        }
    }

    internal static List<string> ChunkText(string text, int targetSize = TargetChunkSize)
    {
        var paragraphs = Regex.Split(text.Trim(), @"\r?\n\s*\r?\n")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (current.Length > 0 && current.Length + paragraph.Length > targetSize)
            {
                chunks.Add(current.ToString().Trim());
                current.Clear();
            }

            if (paragraph.Length > targetSize)
            {
                foreach (var sentence in SplitSentences(paragraph))
                {
                    if (current.Length > 0 && current.Length + sentence.Length > targetSize)
                    {
                        chunks.Add(current.ToString().Trim());
                        current.Clear();
                    }

                    current.Append(sentence).Append(' ');
                }
            }
            else
            {
                current.Append(paragraph).Append("\n\n");
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString().Trim());
        }

        return chunks;
    }

    private static IEnumerable<string> SplitSentences(string paragraph) =>
        Regex.Split(paragraph, @"(?<=[.!?])\s+")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);
}
