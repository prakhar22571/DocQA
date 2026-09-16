using Microsoft.SemanticKernel;

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

    return kernelBuilder.Build();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
