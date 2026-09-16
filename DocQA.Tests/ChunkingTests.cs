namespace DocQA.Tests;

public class ChunkingTests
{
    [Fact]
    public void ChunkText_SplitsOnParagraphAndSentenceBoundaries()
    {
        var longSentence = "This is one sentence in a very long paragraph that should force " +
                            "sentence-level splitting because it exceeds the target chunk size by itself.";
        var longParagraph = string.Join(" ", Enumerable.Repeat(longSentence, 5));

        var text = "This is the first paragraph. It has a couple of sentences in it to test grouping behavior nicely.\n\n" +
                   "This is a second, short paragraph.\n\n" +
                   longParagraph + "\n\n" +
                   "Final short paragraph to wrap things up.";

        var chunks = IngestionService.ChunkText(text);

        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 500, $"Chunk exceeded target size: {chunk.Length} chars"));

        // The two short paragraphs group into the first chunk (paragraph boundary).
        Assert.Contains("This is the first paragraph.", chunks[0]);
        Assert.Contains("This is a second, short paragraph.", chunks[0]);

        // The oversized paragraph is split on sentence boundaries across the remaining chunks.
        Assert.Contains(longSentence, chunks[1]);
        Assert.Contains(longSentence, chunks[2]);

        // The final short paragraph joins the trailing chunk rather than starting a new one.
        Assert.Contains("Final short paragraph to wrap things up.", chunks[2]);
    }
}
