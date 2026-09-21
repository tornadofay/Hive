# Hive — Current Status

Last updated: 2026-09-21

## Repository state

Phase 0.1 through 0.5 are complete. The active implementation slice is Phase 0.6 WinForms UI/UX Foundation. The developer has verified the full Hive.Tests suite after the 0.5 test-harness changes.

## Current phase

Phase 0 — Foundations.

**Active slice: 0.6 — WinForms UI/UX Foundation.**

## Architecture decisions now locked

- Phase 0 establishes a shared WinForms UI foundation using ReaLTaiizor as the selected third-party rendering layer behind Hive.Host.WinForms.UI; consuming forms do not reference ReaLTaiizor directly.
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
- The current Example startup form is a 0.6 UI-foundation verification surface. It is not the Phase 0.7 Example Host Shell; 0.7 remains intentionally not started until 0.6 verification is complete.
- Authentication-provider selection is deferred until real multi-user requirements reach Phase 8.
- Tests and examples are developed with each implementation slice; no unperformed verification is claimed.
- Phase 0.3 identity/resource contracts use explicit typed identity, owner, scope, provenance, version, lifecycle, and WorkItem state. Scope matching is a structural boundary and does not itself grant authorization.

## Current implementation progress

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

- Phase 0.7 Example Host Shell.
- Phase 0.8 Example Developer Test Tools.
- Later implementation slices.
- WinForms management host.
- Example application features.

0.5 is complete and verified. 0.6 is active and awaiting developer UI verification.