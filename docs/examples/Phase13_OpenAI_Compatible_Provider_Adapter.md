# Phase 1.3 — OpenAI-compatible Provider Adapter

Use one shared transport adapter for compatible hosted or local OpenAI-compatible endpoints.

Example:

\`\`\`csharp
using Hive.Core;
using Hive.Providers.OpenAICompatible;

using var httpClient = new HttpClient();
using var apiKey = SecretMaterial.Create("api-key");

var adapter = new OpenAICompatibleProviderAdapter(
    httpClient,
    new OpenAICompatibleProviderOptions(
        new Uri("https://example.test/v1/"),
        apiKey));

var result = await adapter.CompleteChatAsync(
    new OpenAICompatibleChatRequest(
        "example-model",
        [
            new OpenAICompatibleMessage(
                OpenAICompatibleMessageRole.System,
                "You are a concise assistant."),
            new OpenAICompatibleMessage(
                OpenAICompatibleMessageRole.User,
                "Return a short greeting.")
        ]),
    cancellationToken);
\`\`\`

For JSON structured output:

\`\`\`csharp
using var schema = JsonDocument.Parse(
    """{"type":"object","properties":{"name":{"type":"string"}}}""");

var result = await adapter.CompleteChatAsync(
    new OpenAICompatibleChatRequest(
        "example-model",
        [
            new OpenAICompatibleMessage(
                OpenAICompatibleMessageRole.User,
                "Return the requested data.")
        ],
        new OpenAICompatibleStructuredOutput(
            "example",
            schema.RootElement)),
    cancellationToken);
\`\`\`

Contract:
- Base URI must be absolute HTTP/HTTPS and must not contain credentials.
- API key is supplied as \`SecretMaterial\`; the adapter does not persist or own it.
- The adapter sends \`POST <base-uri>/chat/completions\`.
- Successful responses expose assistant text.
- Structured requests also expose parsed \`JsonElement\` content.
- \`401/403\` → \`Unauthorized\`.
- \`429\` → \`External\` with the rate-limit error code.
- provider timeout → \`Timeout\`.
- caller cancellation propagates as OperationCanceledException.
- network transport failure → External.
- malformed provider response or malformed structured JSON → Serialization.
- each chat request is limited to 256 messages.
- model identifiers are limited to 512 characters.
- serialized provider request bodies are limited to 4 MiB and return a structured validation failure when exceeded.
- successful provider response bodies are limited to 4 MiB; oversized responses return a structured serialization failure.
- message content preserves caller-supplied leading/trailing whitespace; whitespace-only content remains invalid.
- the MAF-facing text chat bridge rejects non-text message content as Unsupported rather than silently dropping it.
- the MAF-facing text chat bridge rejects an invalid default model at construction rather than deferring the failure to execution.
- per-call model selections that exceed the same 512-character model limit return a structured validation error instead of leaking the lower-level argument exception.
- connection testing validates the Provider → ProviderAccount → ExecutionTarget relationship before making a provider request.
- Compatible providers are configurations of the shared adapter; do not add provider-specific transport implementations.
The adapter receives already-resolved credential material and does not own or dispose it. ProviderAccount/SecretReference wiring is added by later management/execution slices.
