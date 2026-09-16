# DocQA

A minimal document Q&A API built on ASP.NET Core and Semantic Kernel. You ingest raw text, it gets
chunked, embedded, and stored in an in-memory vector store; you ask a question, and a chat model
decides for itself whether it needs to search the knowledge base before answering.

## What it does

- **Ingestion**: text is split into ~500-character chunks on paragraph/sentence boundaries, embedded
  via OpenRouter, and upserted into an `InMemoryVectorStore` collection (`document-chunks`).
- **Retrieval-augmented Q&A**: questions are answered by a chat model that has a `SearchKnowledgeBase`
  function available to it. The model calls that function only when it decides it needs context —
  there's no hardcoded "always retrieve" step.
- **Audit trail**: every kernel function invocation (name, arguments, truncated result) is logged to
  the console via an `IFunctionInvocationFilter`, and the query endpoint reports back which functions
  were actually called.

Data is in-memory only — nothing persists across restarts.

> **Embedding dimension: 2048.** `DocumentChunk.Embedding` is sized for
> `nvidia/nemotron-3-embed-1b:free`'s native output (previously 1536, for OpenAI's
> `text-embedding-3-small`). This is a breaking schema change for the in-memory store — old
> 1536-dim vectors are incompatible and aren't migrated. Since storage is in-memory only, this just
> means: restart the app and re-ingest your documents after upgrading.

## Running it

Requires the .NET 10 SDK.

```
dotnet run
```

### Required configuration

The app talks to [OpenRouter](https://openrouter.ai) (an OpenAI-compatible API at
`https://openrouter.ai/api/v1`) rather than OpenAI directly, using free-tier models only:

| Key                          | Purpose                | Default                              |
|-------------------------------|-------------------------|----------------------------------------|
| `OpenRouter:ApiKey`            | Your OpenRouter API key (**required**) | *(none — must be set)*      |
| `OpenRouter:ChatModel`          | Chat completion model  | `nvidia/nemotron-3-super-120b-a12b:free` |
| `OpenRouter:EmbeddingModel`     | Embedding model         | `nvidia/nemotron-3-embed-1b:free`      |

**The API key must be set via the `OPENROUTER_API_KEY` environment variable** — it's read into
configuration explicitly at startup (not via the standard `OpenRouter__ApiKey` double-underscore
convention) and is never hardcoded anywhere in the repo:

```
$env:OPENROUTER_API_KEY = "sk-or-..."      # PowerShell
export OPENROUTER_API_KEY="sk-or-..."      # bash
```

`ChatModel` and `EmbeddingModel` can still be overridden via `appsettings.json`,
`appsettings.Development.json` (gitignored), or ordinary configuration if you want to point at
different OpenRouter models.

OpenRouter's free-tier lineup changes over time — models get added, moved to paid-only, or
deprecated. If ingestion or queries start failing with a `404` mentioning the model slug, check
the currently free, tool-calling-capable models at
[openrouter.ai/models?fmt=cards&supported_parameters=tools&max_price=0](https://openrouter.ai/models?fmt=cards&supported_parameters=tools&max_price=0)
and update `OpenRouter:ChatModel` accordingly (tool calling is required for `SearchKnowledgeBase`
to work).

#### Known constraint: free-tier rate limits

The default models are OpenRouter's free tier, which is rate-limited to **20 requests/minute and 50
requests/day** per key (shared across chat and embedding calls). A hosted demo can hit this quickly —
each `/documents/ingest` call costs one embedding request per chunk, and each `/documents/query`
call costs at least one chat completion request plus one embedding request if the model searches.
If you see `429` errors from OpenRouter, this is why.

Free-tier providers are also occasionally overloaded, which OpenRouter sometimes reports as an
HTTP 200 response with an error-shaped body rather than a proper error status — a shape the OpenAI
client SDK doesn't parse cleanly. `/documents/query` retries up to 3 times on this specific failure
before returning `503`.

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

Neither test calls the real OpenRouter API — both use a deterministic in-process fake
(`FakeTextEmbeddingGenerationService`, producing 2048-dim vectors to match the current schema) in
place of the embedding service, so they run offline and don't require an API key. Note that the
integration test exercises `SearchKnowledgeBase` and the vector store directly, not the chat
completion / tool-calling path — confirming that the configured chat model actually chooses to
call `SearchKnowledgeBase` requires a live call through `/documents/query` with a real
`OPENROUTER_API_KEY`.
