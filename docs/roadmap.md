# Hive — Roadmap (ordered, slice-level implementation path)

This is the granular companion to `docs/architecture.md` Section 8. Each slice is sized to be one GPT session (run-bounded execution). When a slice starts, copy its Objective/Files/Verify into `docs/Hive_Active_Work.md` as the working scratchpad for that session.

Numbering is `Phase.Slice` (for example `1.7`) — an ordinal sequence within a phase, not a version string.

Phases 0–1 are detailed enough to build from directly. Phases 2–9 stay lighter until the project reaches them.

---

## Phase 0 — Foundations

### 0.1 — Solution & project scaffolding

**Objective:** create the `.sln` and all empty projects (`Hive.Core`, `Hive.Agents`, `Hive.Persistence`, `Hive.Coordination`, `Hive.Tools`, `Hive.Providers.OpenAICompatible`, `Hive.Management`, `Hive.Host.WinForms`, `Hive.Tests`) with correct reference directions.

**Files:** `Hive.sln`, each project's `.csproj`, one placeholder class each.

**Verify:** `dotnet build` succeeds; inspect `Hive.Host.WinForms.csproj` to confirm it references only `Hive.Management`, not Core/Persistence directly.

### 0.2 — Common infrastructure

**Objective:** implement IDs, base error/result types, an `IClock` time abstraction, a base event envelope type, and lock in `System.Text.Json`.

**Files:** `Hive.Core/Foundation/*`.

**Verify:** unit tests for ID equality, a fake `IClock` usable in tests, JSON round-trip of a sample event envelope.

### 0.3 — Generic Resource base model

**Objective:** implement `Resource` (Identity/Owner/Scope/Version/Provenance/Lifecycle/Permissions/Metadata) and the canonical `ResourceScope` enum (`Global/Tenant/User/Workspace/Agent/Runtime/Execution`).

**Files:** `Hive.Core/Resources/*`.

**Verify:** unit test confirming a sample derived resource carries all fields and fails closed when identity is missing.

### 0.4 — Database bootstrap

**Objective:** LocalDB connection, a migration tool (DbUp is the current recommendation), create Hive's own database, first migration.

**Files:** `Hive.Persistence/Migrations/*`, connection configuration.

**Verify:** running the migration tool creates the database with the placeholder table; confirm via direct query.

### 0.5 — Test harness

**Objective:** scaffold `Hive.Tests` (xUnit is the current recommendation) with one trivial passing test.

**Files:** `Hive.Tests/*`.

**Verify:** `dotnet test` shows 1 passing test.

---

## Phase 1 — Multi-Agent Pipeline MVP

### 1.1 — Provider / ProviderAccount / Execution Target schema

**Objective:** these three as concrete `Resource`-derived types with persistence, including the three-state capability field.

**Files:** `Hive.Core/Providers/{Provider,ProviderAccount,ExecutionTarget,Capability}.cs`, `Hive.Persistence/Providers/*`.

**Verify:** unit test — create a Provider, add a ProviderAccount, query both back.

### 1.2 — `ISecretStore`

**Objective:** encrypt/decrypt via Windows DPAPI; a redaction helper for logging.

**Files:** `Hive.Core/Security/ISecretStore.cs`, `Hive.Persistence/Security/DpapiSecretStore.cs`.

**Verify:** unit test — store a fake key, decrypt it back, and confirm the raw DB row does not contain the plaintext.

### 1.3 — OpenAI-compatible adapter, basic chat call

**Objective:** the one shared adapter's non-streaming chat completion, parameterized by base URL + key.

**Files:** `Hive.Providers.OpenAICompatible/OpenAICompatibleChatClient.cs`.

**Verify:** automated test against a local fake HTTP server; one manual smoke test against a real provider.

### 1.4 — Capability-aware target selection

**Objective:** mandatory hard filter — select an Execution Target that supports a required capability; fail clearly if none qualify.

**Files:** `Hive.Core/Execution/ExecutionTargetSelector.cs`.

**Verify:** unit tests for a supported match, an unsupported exclusion, and a no-qualifying-target typed error.

### 1.5 — Agent identity core types + Factory

**Objective:** `AgentDefinition` / `RuntimeInstance` / `Execution` and `AgentFactory.CreateIncarnation`.

**Files:** `Hive.Agents/{AgentDefinition,RuntimeInstance,Execution,AgentFactory}.cs`.

**Verify:** unit test — Factory creates a first incarnation with empty initial state.

### 1.6 — EventLog + Snapshot + transactional Outbox

**Objective:** append-only event log, snapshot fold, outbox row written in the same transaction as the triggering event.

**Files:** `Hive.Persistence/Events/{EventLogStore,SnapshotStore,OutboxStore}.cs`.

**Verify:** unit test appending events + outbox row in one transaction; forced rollback confirms neither persists.

### 1.7 — Outbox poller

**Objective:** background sweep processing unprocessed outbox rows (stub handler for now; Phase 3 builds the real postmortem behavior).

**Files:** `Hive.Agents/Lifecycle/OutboxPoller.cs`.

**Verify:** manual test — insert a row directly, confirm the poller processes it; kill the process before processing and confirm a restart still picks it up.

### 1.8 — First real agent execution, single step

**Objective:** wire one MAF agent to Hive's `IChatModelProvider`, execute one request, record the resulting event(s).

**Files:** `Hive.Agents/Execution/AgentExecutor.cs`.

**Verify:** manual smoke test — send a simple request, confirm a real response returns and an event is written.

### 1.9 — Document parsing (text-native formats)

**Objective:** extract text from Word/Excel/text-based PDFs.

**Files:** `Hive.Tools/Ingestion/DocumentParser.cs`.

**Verify:** unit test against a checked-in sample file, confirming expected extracted text.

### 1.10 — Vision routing for scanned/image content

**Objective:** rasterize/prepare non-text-extractable pages and route through a Vision-capable target.

**Files:** `Hive.Tools/Ingestion/ImageRouter.cs`.

**Verify:** manual smoke test with one real scanned sample.

### 1.11 — Structured Output extraction

**Objective:** extract a typed record; fields depend on the first document type selected.

**Files:** `Hive.Agents/Extraction/StructuredExtractor.cs`, the schema type.

**Verify:** test against a fixed sample input, confirming the returned object matches the schema.

### 1.12 — Validation

**Objective:** required-field/type/range checks before a record is eligible for the write step.

**Files:** `Hive.Agents/Extraction/Validator.cs`.

**Verify:** unit tests for valid and invalid records.

### 1.13 — Business-app write Tool, approval-gated

**Objective:** the Tool proposes a write, holds `PendingApproval`, executes only after explicit approval.

**Files:** `Hive.Tools/BusinessApp/WriteRecordTool.cs`, `IApprovalGate`.

**Verify:** unit test — pending state blocks execution; simulated approval releases it and hits a fake business-app client.

### 1.14 — MAF Sequential orchestration for the whole pipeline

**Objective:** wire ingest → extract → validate → write as one MAF Sequential workflow.

**Files:** `Hive.Coordination/Pipelines/DocumentPipeline.cs`.

**Verify:** manual end-to-end run on one real sample document, pausing for approval at the write step.

### 1.15 — `Hive.Management` facade: Providers + Agents

**Objective:** CRUD service layer for Provider/ProviderAccount and AgentDefinition.

**Files:** `Hive.Management/{ProviderManagementService,AgentManagementService}.cs`.

**Verify:** unit tests for create/read/update on both.

### 1.16 — `HiveSettingsForm` + `HiveConfigurationContext` + Providers page

**Objective:** actual WinForms shell plus Providers page (add provider/account, enter/test a key).

**Files:** `Hive.Host.WinForms/Forms/HiveSettingsForm.cs`, `Hive.Host.WinForms/UI/Configuration/{HiveConfigurationContext,Providers/ProvidersPage}.cs`.

**Verify:** manual UI test — add a real provider account and click Test Connection.

### 1.17 — Full-pipeline crash/resume test

**Objective:** verify Phase 1 crash safety end to end.

**Files:** none (verification-only slice).

**Verify:** kill the WinForms process mid-pipeline, relaunch, confirm resume from the last checkpoint and that pending outbox work still gets processed.

### 1.18 — Metrics, budget cap, OpenTelemetry

**Objective:** request/success/failure/timeout counters, token/cost tracking, hard per-incarnation budget, OpenTelemetry console exporter.

**Files:** `Hive.Core/Observability/*`, `Hive.Agents/Execution/BudgetGuard.cs`.

**Verify:** unit test — tiny configured limit halts execution with a typed error; visually confirm telemetry output during a manual run.

---

## Phase 2 — Persistence Hardening

- 2.1 Event schema versioning/upcasting
- 2.2 Snapshot cadence
- 2.3 Generic Resource audit

## Phase 3 — Death / Postmortem / Reincarnation

- 3.1 Evidence extraction
- 3.2 Candidate deduction generation
- 3.3 Learning Review page
- 3.4 `AgentFactory.Reincarnate`
- 3.5 Real postmortem trigger replacing the Phase 1 outbox stub

## Phase 4 — Hive Membership & Coordination

- 4.1 `HiveDefinition` + membership/roles
- 4.2 Configurable agent membership
- 4.3 Shared claims-with-provenance store
- 4.4 Supervisor controls

## Phase 5 — Hive Governance Patterns

- 5.1 Manager-led strategy
- 5.2 Democratic/Voting strategy + first voting rule
- 5.3 Adversarial/Critique strategy + conflict-resolution rule
- 5.4 Strategy-selection UI

## Phase 6 — Cognitive Safety

- 6.1 Goal embeddings + `VECTOR_DISTANCE` drift check
- 6.2 Credit assignment
- 6.3 AGM-style belief revision

## Phase 7 — Multi-Tenancy & Scale

- 7.1 Real authentication
- 7.2 Re-evaluate distributed execution need
- 7.3 `Hive.Host.Web` or `.Wpf` on the same management facade

## Phase 8 — Tooling & Extensibility

- 8.1 MCP-based tool registration
- 8.2 Per-agent tool permission enforcement audit

## Phase 9 — Observability & Ops

- 9.1 Full metrics taxonomy + dashboards
- 9.2 CI/CD pipeline
- 9.3 Replay-based regression tests off the event log
