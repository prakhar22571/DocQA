using DocQA;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiKey = config["OpenAI:ApiKey"]
        ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");
    var chatModel = config["OpenAI:ChatModel"] ?? "gpt-4o-mini";
    var embeddingModel = config["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";

    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(chatModel, apiKey);
#pragma warning disable CS0618 // AddOpenAITextEmbeddingGeneration is obsolete in favor of AddOpenAIEmbeddingGenerator, but ITextEmbeddingGenerationService is still what SK's text-search/memory APIs consume.
    kernelBuilder.AddOpenAITextEmbeddingGeneration(embeddingModel, apiKey);
#pragma warning restore CS0618

    var kernel = kernelBuilder.Build();

    var collection = sp.GetRequiredService<VectorStoreCollection<string, DocumentChunk>>();
    kernel.Plugins.AddFromObject(new KnowledgeBasePlugin(kernel, collection), "KnowledgeBase");

    kernel.FunctionInvocationFilters.Add(new FunctionInvocationLoggingFilter());

    return kernel;
});

builder.Services.AddSingleton<InMemoryVectorStore>();
builder.Services.AddSingleton<VectorStoreCollection<string, DocumentChunk>>(sp =>
    sp.GetRequiredService<InMemoryVectorStore>().GetCollection<string, DocumentChunk>("document-chunks"));
builder.Services.AddSingleton<IngestionService>();

// Default execution settings so chat completion callers get automatic function calling
// (the model decides whether to invoke SearchKnowledgeBase) without repeating this per call site.
builder.Services.AddSingleton(new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/documents/ingest", async (IngestRequest request, HttpContext context) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest("Text must not be empty or whitespace.");
    }

    var ingestionService = context.RequestServices.GetRequiredService<IngestionService>();
    await ingestionService.IngestAsync(request.SourceDocument, request.Text);
    return Results.Ok();
});

app.MapPost("/documents/query", async (QueryRequest request, HttpContext context) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest("Question must not be empty or whitespace.");
    }

    var kernel = context.RequestServices.GetRequiredService<Kernel>();
    var executionSettings = context.RequestServices.GetRequiredService<OpenAIPromptExecutionSettings>();
    var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

    var history = new ChatHistory("""
        You are a knowledge base assistant. Answer the user's question using only information
        retrieved from the knowledge base via the SearchKnowledgeBase function. Call
        SearchKnowledgeBase when you need context to answer. If the knowledge base has no
        relevant information, respond exactly with "I don't have that information." Do not use
        outside knowledge.
        """);
    history.AddUserMessage(request.Question);

    var response = await chatCompletionService.GetChatMessageContentAsync(history, executionSettings, kernel);

    var functionCallsMade = history
        .SelectMany(message => message.Items.OfType<FunctionCallContent>())
        .Select(call => call.FunctionName)
        .ToList();

    return Results.Ok(new QueryResponse(response.Content ?? string.Empty, functionCallsMade));
});

await app.Services.GetRequiredService<VectorStoreCollection<string, DocumentChunk>>()
    .EnsureCollectionExistsAsync();

app.Run();

record IngestRequest(string SourceDocument, string Text);
record QueryRequest(string Question);
record QueryResponse(string Answer, IReadOnlyList<string> FunctionCallsMade);
