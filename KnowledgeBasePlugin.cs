using System.ComponentModel;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace DocQA;

public class KnowledgeBasePlugin(Kernel kernel, VectorStoreCollection<string, DocumentChunk> collection)
{
    [KernelFunction]
    [Description("Searches the knowledge base for chunks relevant to the given query and returns their content together with the source document each came from.")]
    public async Task<string> SearchKnowledgeBase(string query, int topK = 3)
    {
#pragma warning disable CS0618 // ITextEmbeddingGenerationService is obsolete in favor of IEmbeddingGenerator, but it's what AddOpenAITextEmbeddingGeneration registers on the kernel.
        var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query);
#pragma warning restore CS0618

        var matches = new List<string>();
        await foreach (var result in collection.SearchAsync(queryEmbedding, topK))
        {
            matches.Add($"[{result.Record.SourceDocument}] {result.Record.Content}");
        }

        return string.Join("\n\n", matches);
    }
}
