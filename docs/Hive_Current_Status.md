# Hive — Current Status

Last updated: 2026-09-22

## Repository state

Phase 0 — Foundations is complete. Slices 0.1 through 0.5 were completed and verified, 0.6 was accepted through developer UI interaction, and 0.7 was completed and accepted as the final Example Host/UI polish slice. Slice 0.8 was removed before Phase 0 closure because it is no longer needed. Phase 1.1 through Phase 1.3 are complete and verified; Phase 1.4 is now the active implementation slice.

## Current phase

Phase 0 — Foundations: **Complete**.

**Active slice: 1.4 — Capability-aware Execution Target Selection.**

## Architecture decisions now locked

- Phase 0 establishes a shared WinForms UI foundation using Hive-owned controls, native WinForms controls, and custom System.Drawing rendering behind Hive.Host.WinForms.UI; no third-party rendering dependency is currently used.
- Hive owns its UI contract, theme modes, semantic design tokens, and Hive-specific controls where additional behavior/styling is needed; it does not wrap every WinForms control merely to rename it.
- Hive.Example.WinForms is a first-class permanent developer-facing project from Phase 0, with scalable Category → Subcategory → Example navigation and external dotnet test developer tooling.
- Hive is general-purpose; the real V1 forcing function is automating data entry from documents/images into the existing business application.
- The V1 pipeline is not Hive's permanent definition; it determines implementation order.
- Agent and Hive are stable base types.
- The base Agent may provide reusable Objectives, memory infrastructure, Question/Answer transport, Patience / Understanding Gate, Simulation infrastructure, delegation, and Hive sponsorship without becoming a CognitiveAgent.
- Agent generation is fixed at creation. Authorized creators may request any supported generation, including CognitiveAgent; generation is never inferred or promoted automatically.
- Generation and Hive membership are independent; base Hives may contain CognitiveAgents and base Agents may sponsor Hives.
- Hive sponsorship is not lifecycle ownership. A Hive and its independent members survive sponsor runtime death/retirement/deletion unless explicitly retired.
- Swarm is a non-persistent active subset of Hive members collaborating on a bounded problem; it is not another architectural resource/lifecycle layer.
- Workspace is the human-facing operational surface over Hive.Management. V1 is limited to image submission, WorkItem status/activity, relevant execution/provider status, notifications, and business-app write Approve/Reject. Agent/Hive topology, Swarm views, general LLM mode, and Agentic mode are later phase-gated Workspace extensions.
- V1 WorkItem semantics are fixed: one submitted document is one WorkItem; batches are multiple WorkItems.
- Durable events carry event type and payload schema version with an upcasting compatibility boundary separate from database schema versioning.
- Dream processing is bounded by applicable authorization, quota, cost/token, time, concurrency, retrieval/work, and cancellation policies.
- A base Agent may sponsor a persistent Hive for multi-specialty work; a Swarm is the active work session and a Hive may become Dormant afterward.
- Hive population authority belongs to the Hive during Hive-managed work; member Agents normally request missing specialties rather than recursively creating child Hives.
- CognitiveAgent : Agent and CognitiveHive : Hive are later additive generations.
- Concrete type is selected at creation; there is no runtime promotion/demotion.
- Different Agent/Hive generations can coexist without ancestor contract changes.
- Persistent cognition belongs to the CognitiveAgent generation rather than being a prerequisite of the base Agent.
- Hive uses MAF for execution/orchestration mechanisms MAF already provides.
- One shared OpenAI-compatible provider adapter serves compatible providers/local servers through configuration.
- V1 provider configurations currently targeted: Groq, OpenRouter, Cloudflare, Cerebras, NVIDIA, Google, and local OpenAI-compatible servers.
- V1 business-app integration explicitly supports both API/service and bounded UI integration; they are not mutually exclusive and may be used together per WorkItem or operation.
- The first V1 document/input type is an image.
- V1 WinForms host discovery covers the relevant Form/control hierarchy, including Forms, UserControls, custom/inherited controls, Panels, GroupBoxes, other containers, nested controls, and relevant runtime/data-source context; discovery never grants action authority.
- CognitiveAgent persists its cognition independently of any one runtime incarnation; death ends the incarnation, not the Agent or its durable state.
- Dreams are bounded offline cognitive simulations/analysis that can run with no live Agent runtime, including during host-application downtime; Dream output remains distinct from actual experience.
- Questions are first-class, specialty-aware cognitive objects that can be owned by individual CognitiveAgents and coordinated collectively by CognitiveHive.
- CognitiveHive adds collective cognition without moving or replacing member-level cognition.
- Generic cross-host integration is deferred until a second real host proves the need to generalize V1 patterns.
- V1 human intervention is Approve/Reject at the business-app write; the broader intervention taxonomy is later.
- Developer manual testing is the current UI/application verification approach; no UI-automation framework is required by the architecture.
- Reusable WinForms data-page composition remains a presentation boundary: HiveListPageLayout and HivePaginationBar are generic UI primitives, HiveCrudPage<TItem> adds generic CRUD interaction orchestration without domain or persistence knowledge, and HiveEditorLayout provides reusable labeled-field/action-footer composition. The reusable CRUD page also provides consistent search presentation, loading/empty/no-match states, keyboard interaction, contextual actions, and compact count/status feedback. Feature-specific columns, filtering semantics, validation, specialized editors, authorization, and persistence remain outside Hive. `ListView` is the lightweight default list surface, while `DataGridView` remains available for richer tabular cases.
- Example UI is separately centralized for the developer-facing test harness: HiveExampleTestSurface is the reusable standard Example page surface for the repeated HAgent-style Run/copy actions, editable input, copyable C# reproduction snippet, description, expected-result, note, status, cancellation, and exception handling; HiveExampleOutputView is one persistent global bottom output pane shared by examples through IServiceProvider, with Show/Hide and Clear behavior. This is optional composition; specialized examples such as CRUD and dialog demonstrations are free to keep their own UI.
- WinForms DPI scaling is delegated to the .NET 10/WinForms platform; Hive does not maintain a custom DPI helper or manual DPI scaling layer.
- The 0.6 UI foundation is now the shared rendering/composition layer for later WinForms features.
- Phase 0.7 established the permanent Category → Subcategory → Example Example Host shell and completed the final production UI/UX polish of the shared WinForms foundation and Example host. The developer has accepted the slice; no further Phase 0 UI work is active.
- Authentication-provider selection is deferred until real multi-user requirements reach Phase 8.
- Tests and examples are developed with each implementation slice; no unperformed verification is claimed.
- Phase 0.3 identity/resource contracts use explicit typed identity, owner, scope, provenance, version, lifecycle, and WorkItem state. Scope matching is a structural boundary and does not itself grant authorization.

## Current implementation progress

### Phase 1.1 — Provider / ProviderAccount / ExecutionTarget

Complete and verified.

Developer verification:
- Hive.Example.WinForms Provider Platform scenario completed successfully, including migration/schema 2, Provider/ProviderAccount/ExecutionTarget CRUD, capability-state display, ownership/scope failures, and retirement.
- Full Hive.Tests execution: **62 tests passed, 0 failed, 0 skipped in 1.6 seconds**.
- The final 1.1 corrections were re-tested successfully; the 1.1 completion gate is satisfied.

### Phase 1.2 — Secret Store

Complete and verified.

Developer verification:
- Hive.Example.WinForms DPAPI Secret Store scenario completed successfully: schema 3, secret creation/version 1, redaction, ownership/scope rejection, replacement/version 2, hard deletion, and post-delete NotFound.
- Full Hive.Tests execution: **67 tests passed, 0 failed, 0 skipped in 1.8 seconds**.
- The 1.2 completion gate is satisfied.


### Phase 1.3 — OpenAI-compatible Provider Adapter

Complete and verified.

Developer verification:
- Hive.Example.WinForms `Providers / Provider Transport / OpenAI-compatible Provider Adapter` completed successfully against the local fake HTTP endpoint, including normal and structured-output responses.
- Full `Hive.Tests` execution: **80 tests passed, 0 failed, 0 skipped in 1.6 seconds**.
- The 1.3 completion gate is satisfied.

### Phase 0 closure — Foundations

Complete. Phase 0.7 was the final active slice and is accepted after the developer's final UI/UX review. No 0.8 slice remains.


### Phase 0.3 — Identity, WorkItem & Resource foundation

Implemented in `Hive.Core`:

- eleven typed identity/value types: Deployment, Tenant, Principal, User, Session, Workspace, Agent, Hive, Runtime, Execution, and WorkItem;
- ResourceKind inventory classification;
- ResourceScope / ResourceAccessContext and explicit scope matching;
- ResourceVersion, ResourceLifecycle, ResourceProvenance, ResourceReference;
- immutable ResourceEnvelope<TIdentity> and identity snapshots;
- WorkItem status/lifecycle/version transitions;
- focused 0.3 contract tests;
- copyable 0.3 public API example under `docs/examples`.

Developer verification: **36 tests passed, 0 failed, 0 skipped in 1.5 seconds.** Full-solution rebuild and application launch were also reported successful.

### Phase 0.4 — Persistence bootstrap

Completed in Hive.Persistence:
- SQL Server/LocalDB database configuration with automatic database creation enabled by default;
- DbUp SQL Server 7.2.0 migration runner;
- Microsoft.Data.SqlClient 7.1.0;
- Hive-owned schema-version tracking and DbUp journal configuration;
- future-schema compatibility rejection;
- transaction-per-script migration execution;
- bootstrap metadata schema with primary/unique indexes;
- persistence option tests and SQL integration tests;
- public example under docs/examples/Phase04_Persistence.md.

Developer verification: **44 tests passed, 0 failed, 0 skipped in 3.3 seconds.** The persistence integration tests executed successfully against the developer SQL Server instance and created the Hive test databases.

### Phase 0.5 — Test harness

Complete. The reusable test-harness infrastructure is implemented and verified.

Developer verification: **52 tests passed, 0 failed, 0 skipped in 1.6 seconds.** The normal Visual Studio Hive.Tests workflow completed successfully.

Implemented so far:
- reusable FakeClock;
- deterministic test-only FakeProvider;
- centralized PersistenceTestDatabase helper;
- deterministic EventTestData factory;
- existing clock/event tests migrated to the shared helpers;
- focused tests for clock, provider, and event test infrastructure;
- no new production provider abstraction introduced.

## Completed

### Phase 0.3 — Identity, WorkItem & Resource foundation

- Implemented the typed identity/resource foundation in `Hive.Core`.
- Added focused 0.3 tests and the public API example.
- Developer ran the complete suite: **36 tests passed, 0 failed, 0 skipped in 1.5 seconds.**
- Developer reports the full solution rebuilds successfully and the application launches successfully.
- 0.3 completion gate satisfied.

### Phase 0.1 — Solution & project scaffolding

- Eleven-project .NET 10 solution scaffold created and merged into main.
- Hive.Example.WinForms is the current developer startup project.
- Developer reports a successful full-solution rebuild.
- Developer reports the example application launches successfully with the current placeholder form.

### Phase 0.2 — Common infrastructure

- Common technical IDs, typed Error/Result contracts, IClock, durable event envelope contracts, event payload schema versioning, upcasting registry, and System.Text.Json serialization implemented.
- Initial `Result.cs` compilation defect corrected.
- Focused xUnit contract tests added.
- Developer rebuilt the solution and launched the application successfully.
- Developer ran the complete test suite: **20 tests passed, 0 failed, 0 skipped in 1.3 seconds.**
- 0.2 completion gate satisfied.

## Not started

- Phase 1.4 and later Phase 1 implementation slices.
- Later phases.

Phase 0.1 through 0.5 are complete and verified. Phase 0.6 was accepted after developer manual interaction with the Example UI. Phase 0.7 is complete and accepted. Phase 0 is officially closed. Phase 1.1 through Phase 1.3 are complete and verified; Phase 1.4 is active.