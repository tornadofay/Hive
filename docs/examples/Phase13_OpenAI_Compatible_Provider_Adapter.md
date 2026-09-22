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
- caller cancellation propagates as \`OperationCanceledException\`.
- network transport failure → \`External\`.
- malformed provider response or malformed structured JSON → \`Serialization\`.
- Compatible providers are configurations of the shared adapter; do not add provider-specific transport implementations.

The adapter receives already-resolved credential material. ProviderAccount/SecretReference wiring is added by later management/execution slices.
