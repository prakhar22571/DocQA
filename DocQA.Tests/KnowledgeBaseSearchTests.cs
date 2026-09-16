using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Embeddings;

namespace DocQA.Tests;

public class KnowledgeBaseSearchTests
{
    [Fact]
    public async Task SearchKnowledgeBase_ReturnsIngestedContent_ForRelevantQuery()
    {
        var kernelBuilder = Kernel.CreateBuilder();
#pragma warning disable CS0618 // Stubbing the same (obsolete) service the app registers via AddOpenAITextEmbeddingGeneration.
        kernelBuilder.Services.AddSingleton<ITextEmbeddingGenerationService>(new FakeTextEmbeddingGenerationService());
#pragma warning restore CS0618
        var kernel = kernelBuilder.Build();

        var vectorStore = new InMemoryVectorStore();
        var collection = vectorStore.GetCollection<string, DocumentChunk>("document-chunks");
        await collection.EnsureCollectionExistsAsync();

        var ingestionService = new IngestionService(kernel, collection);
        await ingestionService.IngestAsync(
            "penguins.txt",
            "Penguins are flightless birds that live almost exclusively in the Southern Hemisphere, especially Antarctica.");
        await ingestionService.IngestAsync(
            "volcanoes.txt",
            "Volcanoes form where magma from within the Earth's mantle works its way to the surface.");

        var plugin = new KnowledgeBasePlugin(kernel, collection);

        var result = await plugin.SearchKnowledgeBase("Tell me about penguins living in Antarctica", topK: 1);

        Assert.Contains("penguins.txt", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Penguins are flightless birds", result);
        Assert.DoesNotContain("volcanoes.txt", result, StringComparison.OrdinalIgnoreCase);
    }
}
