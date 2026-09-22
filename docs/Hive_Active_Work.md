# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.2 — Secret Store**

Phase 0 — Foundations is officially complete, and Phase 1.1 — Provider / ProviderAccount / ExecutionTarget is complete and verified.

Do not introduce later Phase 1 slices until 1.2 is complete.

## Objective

Establish the platform Secret Store boundary required by the architecture:

- DPAPI-backed encrypted secret persistence on Windows;
- a public \`ISecretStore\` contract;
- redacted secret material and diagnostics;
- secure replacement with optimistic version protection;
- hard deletion of encrypted secret rows;
- ownership and scope enforcement at the authoritative Secret Store boundary.

The implementation must preserve the existing Core/Persistence dependency direction, SQL Server persistence boundary, typed identity/resource contracts, and MAF-first architecture.

## 1.2 implementation scope

- Add typed \`SecretId\` identity and \`Secret\` resource contracts.
- Represent secret material separately from the resource envelope so the value is never part of ordinary resource metadata.
- Provide \`SecretMaterial\` with explicit lifetime, redacted \`ToString()\`, size validation, and disposal.
- Add the public \`ISecretStore\` persistence boundary.
- Implement \`SqlDpapiSecretStore\` using Windows DPAPI with \`DataProtectionScope.CurrentUser\`.
- Persist only encrypted secret bytes in Hive's SQL Server database.
- Enforce resource ownership and scope on create/read/replace/delete.
- Replace in place under the same Secret identity while incrementing \`ResourceVersion\`; stale replacement attempts return \`Concurrency\`.
- Physically delete secret rows so deletion does not retain the encrypted credential as an ordinary retired resource.
- Map duplicate SQL identities to \`Conflict\`, malformed/decrypt failures to typed internal errors, and unsupported non-Windows execution to \`Unsupported\`.
- Add the SQL migration and indexes required by the store.
- Add focused contract and persistence tests.
- Add the mandatory Hive.Example.WinForms public scenario.
- Do not implement provider transport, ProviderAccount credential wiring, Management settings UI, external vaults, or later provider-selection/planning behavior in this slice.

## Implementation checkpoint — verification pending

The 1.2 implementation is present in the repository at this checkpoint:

- \`Hive.Core\` exposes \`SecretId\`, \`Secret\`, \`SecretReference\`, and \`SecretMaterial\`.
- \`Hive.Persistence\` exposes \`ISecretStore\` and \`SqlDpapiSecretStore\`.
- SQL schema version is now 3 and adds \`HiveSecrets\` plus owner/scope and key indexes.
- Secret values are protected with Windows DPAPI before SQL persistence and are not returned through resource metadata.
- Replacement uses resource-version concurrency and deletion physically removes the stored row.
- \`Hive.Tests\` contains secret contract and persistence integration scenarios.
- \`Hive.Example.WinForms\` contains the matching DPAPI Secret Store example.
- \`docs/examples/Phase12_Secret_Store.md\` documents the public API.

Agent-run build/tests/manual verification are not authorized. The implementation therefore remains pending the developer verification gate below.

## Verification

Required for completion of 1.2:

1. normal, invalid, and boundary Secret/SecretMaterial contract tests;
2. encryption-at-rest verification against the SQL persistence boundary;
3. ownership and scope enforcement;
4. duplicate identity and malformed encrypted-state cases;
5. replacement/version concurrency and hard-deletion behavior;
6. public API Example verification;
7. broader \`Hive.Tests\` execution;
8. manual Example Host verification of the secret-store scenario and absence of secret material in output.

No verification claim is recorded until it has actually been performed.

## Dependency direction

\`\`\`text
Hive.Core
   ↑
Agents / Persistence / Tools / Providers
   ↑
Management
   ↑
Host.WinForms
   ↑
Host.WinForms.UI

Example.WinForms → public platform contracts + Host.WinForms + Host.WinForms.UI
Tests → projects under test

Coordination may depend on Core + Agents + MAF contracts where required.
No core/platform project may depend on Example.WinForms.
\`\`\`

## Constraints

- Hive.Core remains dependency-light and must not reference SQL Server or DPAPI.
- DPAPI implementation remains inside Hive.Persistence.
- Secret material must never be persisted as plaintext, placed in resource metadata, emitted in diagnostics, or shown by the Example output.
- Use \`DataProtectionScope.CurrentUser\`; do not introduce machine-wide key sharing or an external vault in this slice.
- Do not add provider transport or credential networking.
- Do not start 1.3 or later Phase 1 slices during 1.2.
- Preserve the existing SQL Server/LocalDB persistence strategy.

## Verification handoff

Example to run: Providers / Security / DPAPI Secret Store — Hive.Example.WinForms (net10.0-windows).

Tests to run: tests/Hive.Tests/SecretResourceTests.cs and tests/Hive.Tests/SecretPersistenceIntegrationTests.cs; broader Hive.Tests execution is required by the 1.2 completion gate.

Required manual checks include the Example Host secret-store scenario, redaction/output behavior, ownership/scope failures, replacement version, deletion/Post-delete NotFound, and repeat migration behavior.
