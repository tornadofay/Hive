# Phase 1.2 — Secret Store

The Secret Store exposes a public \`ISecretStore\` boundary and a SQL Server implementation backed by Windows DPAPI.

\`\`\`csharp
var store = new SqlDpapiSecretStore(
    HiveDatabaseOptions.LocalDevelopment());

using var material = SecretMaterial.Create(secretValue);

var created = await store.CreateAsync(
    secret,
    material,
    accessContext,
    cancellationToken);

var read = await store.GetAsync(
    secret.Id,
    accessContext,
    cancellationToken);

using (read.Value!.Material)
{
    var value = read.Value.Material.Reveal();
}

using var replacement = SecretMaterial.Create(newSecretValue);

var replaced = await store.ReplaceAsync(
    secret.Id,
    replacement,
    accessContext,
    created.Value!.Resource.Version,
    cancellationToken);

var deleted = await store.DeleteAsync(
    secret.Id,
    accessContext,
    cancellationToken);
\`\`\`

## Contract behavior

- Secret values are encrypted with Windows DPAPI using \`DataProtectionScope.CurrentUser\` before SQL persistence.
- Secret values are not stored in the resource envelope, metadata, or diagnostics.
- \`SecretMaterial.ToString()\` is always \`[REDACTED]\`.
- Replacement preserves the Secret identity and increments its \`ResourceVersion\`; stale expected versions fail with \`Concurrency\`.
- Deletion physically removes the encrypted row so the persisted secret material is not retained as a normal retired resource.
- Ownership and scope remain authoritative at the Secret Store boundary.
- SQL duplicate-key failures are classified as \`Conflict\`.
- DPAPI use is Windows-only; unsupported platforms return a typed \`Unsupported\` error.

## Verification

The Example Host demonstrates creation, protected read, redaction, ownership/scope rejection, replacement, deletion, and post-delete \`NotFound\` without printing secret material.

The automated tests verify contract boundaries, encrypted-at-rest persistence, replacement/concurrency, ownership/scope, malformed encrypted state, duplicate identity, and hard deletion.
