using Microsoft.Extensions.VectorData;

namespace DocQA;

public record DocumentChunk
{
    [VectorStoreKey]
    public required string Id { get; set; }

    [VectorStoreData]
    public required string SourceDocument { get; set; }

    [VectorStoreData]
    public required string Content { get; set; }

    [VectorStoreVector(1536)]
    public required ReadOnlyMemory<float> Embedding { get; set; }
}
