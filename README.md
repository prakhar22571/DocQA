# DocQA

A minimal document Q&A API built on ASP.NET Core and Semantic Kernel. You ingest raw text, it gets
chunked, embedded, and stored in an in-memory vector store; you ask a question, and a chat model
decides for itself whether it needs to search the knowledge base before answering.

## What it does

- **Ingestion**: text is split into ~500-character chunks on paragraph/sentence boundaries, embedded
  via OpenAI, and upserted into an `InMemoryVectorStore` collection (`document-chunks`).
- **Retrieval-augmented Q&A**: questions are answered by a chat model that has a `SearchKnowledgeBase`
  function available to it. The model calls that function only when it decides it needs context —
  there's no hardcoded "always retrieve" step.
- **Audit trail**: every kernel function invocation (name, arguments, truncated result) is logged to
  the console via an `IFunctionInvocationFilter`, and the query endpoint reports back which functions
  were actually called.

Data is in-memory only — nothing persists across restarts.

## Running it

Requires the .NET 10 SDK.

```
dotnet run
```

### Required configuration

The app reads OpenAI settings from configuration (`appsettings.json`, environment variables, or user
secrets — standard ASP.NET Core config layering). At minimum you need an API key:

| Key                      | Purpose                              | Default                 |
|---------------------------|---------------------------------------|--------------------------|
| `OpenAI:ApiKey`            | Your OpenAI API key (**required**)    | *(none — must be set)*  |
| `OpenAI:ChatModel`         | Chat completion model                 | `gpt-4o-mini`            |
| `OpenAI:EmbeddingModel`    | Embedding model                       | `text-embedding-3-small` |

`appsettings.Development.json` is gitignored, so it's a safe place to drop a real key for local
development:

```json
{
  "OpenAI": { "ApiKey": "sk-..." }
}
```

Or via environment variable (double underscore maps to the nested config key):

```
$env:OpenAI__ApiKey = "sk-..."      # PowerShell
export OpenAI__ApiKey="sk-..."      # bash
```

## Endpoints

### `POST /documents/ingest`

Chunks, embeds, and stores a document.

```json
{ "sourceDocument": "faq.txt", "text": "..." }
```

Returns `200 OK` on success, `400` if `text` is empty or whitespace-only.

### `POST /documents/query`

Asks a question against the knowledge base.

```json
{ "question": "What's our refund policy?" }
```

```json
{
  "answer": "...",
  "functionCallsMade": ["SearchKnowledgeBase"]
}
```

`functionCallsMade` lists the kernel functions the model actually invoked while answering —
empty if it answered without searching. Returns `400` if `question` is empty or whitespace-only.

## The orchestration piece: automatic function calling

The chat completion call is configured with `FunctionChoiceBehavior.Auto()` and given the Kernel
(which has `SearchKnowledgeBase` registered as a plugin function). This means the model itself
decides, per question, whether it needs to call `SearchKnowledgeBase` before it can answer — the
API code never forces a retrieve-then-generate pipeline. A system prompt instructs the model to
answer only from retrieved context and to say "I don't have that information" when the knowledge
base has nothing relevant, so a question the model can't ground in retrieved chunks doesn't get a
made-up answer. This is the "agent decides for itself" behavior: the same endpoint handles both a
question that needs a lookup and one that doesn't, and `functionCallsMade` in the response is how
you can see, after the fact, whether retrieval happened.

## Tests

```
dotnet test
```

- `ChunkingTests` verifies the paragraph/sentence-boundary chunking logic in `IngestionService`
  against a known input.
- `KnowledgeBaseSearchTests` is an integration test: it ingests two short fake documents into a
  real `InMemoryVectorStore` and confirms `SearchKnowledgeBase` returns the relevant one for a
  matching query (and not the irrelevant one).

Neither test calls the real OpenAI API — both use a deterministic in-process fake
(`FakeTextEmbeddingGenerationService`) in place of the embedding service, so they run offline and
don't require an API key.
