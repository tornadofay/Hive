# Hive — Active Work

Last updated: 2026-09-25

## Active slice

**Authorized maintenance slice — Hive.Management / Hive.Persistence final backend audit. Phase 1.14 remains inactive.**

This is a temporary maintenance slice explicitly authorized by the user for another production-grade revision of the Management/Persistence backend boundary. It does not advance the roadmap and does not authorize Phase 1.14 or any later roadmap work.

### Scope

- remove remaining raw exception text from public Management `Error` results;
- reject undefined persisted WorkItem status values during activity reconstruction;
- preserve the bootstrap credential invariant under concurrent configuration-save/removal operations within the Management facade;
- add only focused regression coverage for these discovered defects;
- re-audit `Hive.Persistence` boundaries without introducing unrelated persistence changes;
- do not change SQL schema, migrations, provider transport, orchestration, MAF, host adapters, UI, dependencies, or roadmap phase authorization.

### Scope

- remove public exposure of credential-bearing SQL connection strings from `HiveDatabaseOptions` while preserving Persistence-internal connectivity;
- normalize Persistence-facing SQL/unexpected-error messages so raw exception text from SQL Server, DbUp, or persistence internals is not returned through public `Error` results;
- preserve existing structured error codes/categories and expected validation/concurrency/cancellation semantics unless required for the security boundary;
- add focused regression coverage for the public connection-string boundary and error-message redaction;
- do not change SQL schema, migrations, provider transport, orchestration, MAF integration, host adapters, UI, dependencies, or roadmap phase authorization.

### Verification result

The developer verified the final implementation on 2026-09-25 with **218 tests passed, 0 failed, 0 skipped** in 25.3 seconds on .NET 10.0.1 using xUnit.net VSTest Adapter 3.1.5+1b188a7b0a. The verification archive is `docs/verification/maintenance/hive-persistence-production-baseline-hardening-2026-09-25.md`.

No Example Host verification was required because the slice remained backend/internal and introduced no externally visible host/UI behavior.

### Implementation checkpoint

Implemented on `main` through commit `e22c11a1740ff84a33d14478d9ff93df23c25b3c`:
- `HiveDatabaseOptions.ConnectionString` is no longer public; credential-bearing connection access remains internal to `Hive.Persistence` and `Hive.Tests` receives test-only friend access;
- Persistence SQL, unexpected-state, JSON-persistence, migration, connection-test, Agent/Provider/WorkItem, event/outbox, and outbox-handler failure paths no longer copy raw exception text into public `Error.Message` values;
- `HivePersistenceError` centralizes technical exception redaction while preserving existing error codes/categories and the exception object remains transient rather than being stored in the returned `Error`;
- focused regression coverage now checks the non-public connection-string boundary and verifies that technical exception details are excluded from public Persistence errors.

Verification status: **developer-verified**. The final implementation was verified with 218 passed, 0 failed, 0 skipped tests on 2026-09-25; the maintenance slice is closed.

## Closed maintenance pass — Hive.Persistence Production Baseline Hardening

This maintenance pass is complete and verified. The final code checkpoint is `e22c11a1740ff84a33d14478d9ff93df23c25b3c`; the later documentation-only checkpoint is `a4ced845427efcc7bc0daad6696815ad0b47556d`.

The final audit found no additional defect requiring code changes beyond the credential/error-boundary hardening and the subsequent catch-binding corrections. Phase 1.14 remains inactive.

## Closed maintenance pass — Hive.Core Production Polish

This was a focused backend contract-hardening pass requested directly by the user after completion of the preceding UI/UX maintenance pass. It did not advance the roadmap or authorize Phase 1.14 or later.

### Scope

- src/Hive.Core public value objects, resource contracts, event contracts, selection contracts, WorkItem attachment contracts, and related Core tests;
- correct invariant validation and nullable/public API behavior;
- structured failure/serialization semantics where the current public contract is inconsistent;
- attachment content integrity at the Core content boundary;
- preserve existing public behavior except where the current implementation permits invalid or unsafe contract state;
- no new product capability, provider transport, persistence implementation, orchestration engine, MAF replacement, host/UI work, dependency upgrade, or roadmap-slice implementation.

### Explicitly in scope from the audit

- reject malformed default typed identities in ResourceScope, SecretReference, and selection target references;
- reject invalid enum/resource-kind values in ResourceLifecycle and ResourceReference;
- strengthen EventEnvelope invariant validation, including required IDs and defined payload state;
- preserve structured serialization failure behavior when custom event upcasters fail;
- verify WorkItem attachment bytes against their recorded SHA-256 metadata at the Core content boundary;
- add focused regression tests for each changed contract;
- reject malformed optional resource causation identities and invalid ResourceIdentitySnapshot state;
- reject bootstrap credential references when Windows integrated authentication is selected;
- keep custom upcaster/reducer exception text out of public structured error messages and reject invalid requested event payload versions.

### Explicitly out of scope

- changing the ownership model between Core, Management, Persistence, Providers, Agents, Coordination, or MAF;
- redesigning mutable event registries or adding speculative synchronization/freeze abstractions;
- changing SQL schema, migrations, persistence queries/indexes, provider transport, or host adapters;
- adding future cognitive-generation behavior;
- broad identifier/value-object refactoring merely to remove repeated code.

## Previous slice closure

The preceding UI/UX Production Polish maintenance pass was manually verified by the user on 2026-09-24: the solution compiled successfully and the Example Host and configuration/Settings flow ran successfully with no reported failures. Its automated test run was 193 passed, 0 failed, 0 skipped in 27.2 seconds. That maintenance slice is closed.

## Closed-slice implementation checkpoint

Implemented the identified Core contract-hardening issues:
- ResourceScope now treats default scopes as invalid, validates every non-global scope identity, and ResourceEnvelope rejects an invalid scope;
- ResourceLifecycle rejects invalid enum values;
- ResourceReference rejects invalid ResourceKind values and invalid provenance source references;
- ResourceProvenance rejects malformed optional CausationId values; ResourceIdentitySnapshot validates kind, identity, and version invariants;
- SecretReference validates explicit construction and ProviderAccount rejects malformed default references;
- ExecutionTargetSelectionRequest rejects malformed optional preferred/fixed target identities;
- EventEnvelope rejects missing required IDs, malformed causation identity, and undefined JSON payloads;
- EventUpcasterRegistry converts unexpected custom-upcaster failures into structured EventSerializationException failures without copying exception text into the public error message, rejects undefined upcaster payloads, and validates the requested supported payload version; SerializeEnvelope now rejects null input explicitly;
- EventSnapshotFolder keeps unexpected reducer exception text out of returned Error messages and preserves OperationCanceledException instead of converting cancellation into an internal reducer failure;
- WorkItemAttachmentContent verifies content SHA-256 against recorded metadata in addition to content length;
- WorkItemAttachmentMetadata and WorkItemImageSubmission enforce the existing 200-character persistence boundary for image media types;
- HivePersistenceConfiguration rejects a bootstrap credential reference when Windows integrated authentication is selected and BuildDatabaseName performs bounded normalization without input-sized stack allocation;
- EventType enforces the existing 200-character persistence boundary;
- ExecutionTargetSelectionResult rejects a selected target that is not represented by the selection request;
- ResourceLifecycle validates transition enum values before lifecycle-state transition rules;
- focused regression coverage was added/updated in ResourceFoundationTests, ExecutionTargetSelectionTests, EventInfrastructureTests, WorkItemFoundationTests, and HiveConfigurationTests, while the existing SecretResourceTests and ProviderResourceTests coverage remains in place;
- validity markers remain internal implementation state so they do not become accidental JSON/public serialization fields;
- no provider, persistence, orchestration, MAF, host, UI, dependency, schema, migration, or roadmap changes were introduced.

The production-polish audit implementation is complete and verified.

The developer's 2026-09-24 verification run on commit 031c8e122c3d1937f23d75b3cb981b7f2637a4a8 reported 211 tests with 211 passed, 0 failed, 0 skipped in 24.7 seconds. The final audit revision was subsequently verified by the developer with 216 tests run: 216 passed, 0 failed, 0 skipped in 26.4 seconds on .NET 10.0.1 / xUnit.net VSTest Adapter v3.1.5+1b188a7b0a.

## Verification result

The final audit revision was developer-verified on 2026-09-24/25 with **216 tests passed, 0 failed, 0 skipped** in 26.4 seconds on .NET 10.0.1 / xUnit.net VSTest Adapter v3.1.5+1b188a7b0a. This result covers the final implementation and added regression tests.

No Example Host verification was required because the changes remained Core contract hardening without externally visible UI or product behavior.

The Hive.Core Production Polish maintenance pass is closed.

## Roadmap state

**None — Phase 1.13 complete and verified; Phase 1.14 remains inactive.**

### Next-pass architecture preparation

The documentation now defines the planned Phase 1.14 host-integration boundary and the planned Phase 1.17 business-write/Review lifecycle. This is documentation only; it does not authorize implementation of either phase.

The supplied production host source now establishes the host-side root/child relationship, parent-identity propagation, host-side validation and veto points, save/reload lifecycle, bound child-data editing, multiple edit-surface patterns, generated identity behavior, and child-row in-memory mutation before the parent/business save. These are no longer open semantic questions. Phase 1.14 next-pass implementation must still inspect the exact neutral field/column metadata and value-access behavior required by the adapter, runtime mapping from bound rows to stable identities, generated/computed-field serialization, complete existing-child edit serialization, lookup behavior, and host concurrency/version behavior. Any host action surface remains non-authoritative until its concrete production semantics are established. Direct grid editing, same-form supporting controls, and dedicated editor forms/dialogs are interaction patterns, not authorization grants.

Phase 1.14 is expected to use Hive.Core-defined neutral host-integration ports/contracts with concrete host adapters supplied by application composition. `Hive.Management` owns orchestration/authorization and must not reference the concrete WinForms adapter. The real application host is an adapter target, not a Hive platform dependency. Phase 1.17 is expected to persist a BusinessOperationReceipt/operation-attempt record containing operation disposition and affected host record identities, establish durable operation identity before non-transactional host submission, support safe reconciliation of unknown write outcomes, and provide first-class, policy-governed post-write Review separately from pre-write Approval.

### Closed slice

**1.13 — Image Input & WinForms Host Context**

Phase 1.13 is complete and verified. No later implementation slice is active or authorized in this run.

## Objective

Establish image as the first V1 input boundary and provide the concrete WinForms host-context discovery contract needed by later bounded UI integration.

## Scope

- checked-in image fixture usable by deterministic tests/examples;
- bounded WinForms root registration/discovery;
- Form/UserControl/custom Control/container/nested descendant discovery;
- relevant read-only structural/runtime context;
- cycle-safe and bounded traversal;
- cancellation-aware traversal;
- explicit provenance for discovered host context;
- no control mutation/action authority;
- focused automated coverage and a public Example Host scenario.

Do not implement Phase 1.14 or later work in this run.

## Architectural constraints

- Preserve Hive.Core host neutrality; WinForms-specific discovery belongs in the host integration boundary.
- Do not create a second host context system or duplicate WorkItem image-storage contracts.
- Discovery is contextual/read-oriented only. It never grants permission to click, edit, invoke, or mutate controls.
- Traversal must remain bounded, cycle-safe, cancellation-aware, and deterministic.
- Host registrations and discovered snapshots must have explicit ownership/disposal semantics.
- Use the existing Example Host discovery/navigation pattern.
- No database/provider transport is placed in reusable UI controls.

## Implementation checkpoint

The existing WorkItem image submission/storage contract is already authoritative and must not be duplicated. This slice adds the missing concrete WinForms host-context boundary over native WinForms controls.

Required inspection sources:
- `docs/architecture/v1-host-and-management.md`;
- `docs/architecture/foundations.md`;
- `docs/roadmap.md` 1.13;
- `docs/ui/examples.md`;
- existing WorkItem image contracts and Management facade;
- current Host.WinForms and Example Host composition/lifetime boundaries.

Implemented:
- `HiveWinFormsHostContext` with explicit Form registration and deterministic bounded discovery;
- immutable control/binding metadata snapshots with registration/capture provenance;
- configurable maximum depth, maximum node count, and text-length bound;
- cooperative cancellation and explicit UI-thread requirement;
- duplicate/cycle detection and typed discovery-limit failures instead of silent truncation;
- password/control-text redaction for WinForms password fields;
- no raw Control references or mutation/action methods in the discovered snapshot contract;
- checked-in SVG image fixture and deterministic `WorkItemImageSubmission` validation;
- public Example Host scenario demonstrating both image input validation and WinForms host-context discovery.

## Verification result

Example: `Host / WinForms Integration / Image Input & WinForms Host Context` — Hive.Example.WinForms

Manual result: discovered 7 controls from `System.Windows.Forms.Form`; accepted `Phase13Sample.svg` as a valid `image/svg+xml` submission with 404 bytes.

Automated result: `dotnet test tests/Hive.Tests/Hive.Tests.csproj` — 182 passed, 0 failed, 0 skipped.

Phase 1.13 is closed. See [verification/phase-1/1.13.md](verification/phase-1/1.13.md). The next roadmap slice remains inactive until explicitly authorized.
