# Proposed Rust-First Hive Platform Rebuild

**Status:** Strategic proposal / planning only — not yet adopted and not authorized for implementation.  
**Assessment date:** 2026-10-27  
**Purpose:** Define a possible clean, Rust-first foundation for Hive as a long-lived, extensible AI-agent runtime and business-application integration platform.  
**Authority:** This proposal does not override `AGENTS.md`, `docs/architecture.md`, `docs/roadmap.md`, or `docs/Hive_Active_Work.md`. It creates no roadmap phase and grants no implementation authorization.

## 1. Executive direction

Hive should be designed as a long-lived AI runtime, not merely a desktop application, a chatbot, a provider selector, or a framework-specific agent wrapper.

**Product thesis**

> Hive is an extensible runtime that connects AI models, specialized agents, tools, and business applications. It enables agents to collaborate on persistent tasks using real application context, under explicit permissions, durable state, and human governance.

The desktop Workspace is one way to operate Hive; it is not the runtime itself. Provider APIs, model families, agent frameworks, desktop shells, and host applications will change. Hive's own domain model, runtime contracts, authorization rules, and durable state should not need to be redesigned whenever one of those external technologies changes.

If this direction is adopted, a clean Rust-first implementation is in scope. The current C# codebase should be treated as a source of requirements, proven behavior, test cases, and architectural lessons—not as a structure that the new implementation must preserve mechanically.

This is a replacement-platform proposal, not an incremental port plan. Do not build a second implementation alongside the current one without first deciding the target architecture, repository boundary, migration expectations, and which existing behaviors must be preserved.

## 2. Goals and non-goals

### Goals

- Establish Rust as the authoritative implementation of Hive's runtime, domain behavior, provider platform, agent lifecycle, coordination, tool execution, governance, and Hive-owned persistence.
- Support multiple providers and models through explicit, capability-aware adapters rather than provider-specific logic scattered through the runtime.
- Treat Agents as persistent, configurable actors with definitions, execution state, memory/experience policies, capabilities, and governed collaboration.
- Let agents use one another's specialized capabilities through explicit delegation and information-sharing contracts without silently sharing private memory or authority.
- Integrate with business applications through a bounded, language-neutral host contract that reports application context and exposes only declared capabilities.
- Provide a first-class Workspace that shows the user's active business context, relevant specialist Agents, work state, progress, proposals, and approvals.
- Keep the engine independent of the frontend, desktop shell, and any one host application's UI framework.
- Design for restart recovery, cancellation, concurrency, auditability, safe upgrades, versioned contracts, and eventual alternative deployment modes.
- Keep the implementation modular and testable without prematurely splitting every module into a separately deployed service.

### Non-goals

- Reproduce the existing C# solution structure in Rust merely to achieve language parity.
- Rewrite everything at once without contract gates and end-to-end acceptance criteria.
- Make a model, prompt, Agent, UI element, or host application's control state an authorization authority.
- Give Hive unrestricted reflection, arbitrary host-method invocation, unrestricted SQL, or general-purpose control of business applications.
- Adopt every emerging agent standard, framework, database, or transport pre-emptively.
- Create microservices, distributed execution, a plugin marketplace, or multiple database backends before real requirements justify them.
- Claim that Rust alone guarantees correctness, security, scalability, or long-term maintainability.

## 3. Target technology direction

### 3.1 Rust runtime

Rust is the proposed primary language for Hive's authoritative engine. It provides native deployment and strong compile-time support for memory safety and concurrency discipline. These benefits must be backed by sound API design, testing, careful dependency selection, and runtime security controls.

The runtime must not depend on a Tauri window, React component, WebView2 control, .NET service object, or host-private type to perform its core work.

### 3.2 React + TypeScript Workspace

React + TypeScript is the proposed frontend direction for the Hive-owned Workspace. It should present a coherent place to view and manage active contexts, conversations, Agent activity, WorkItems/jobs, results, approvals, configuration, and diagnostics.

Localization foundations must exist from the beginning: stable message keys, runtime language selection, direction-aware layout, and real RTL support for Arabic as well as LTR languages. Visible strings must not be scattered as hard-coded assumptions throughout the UI.

### 3.3 Desktop shell decision

**Tauri is the preferred candidate for a standalone desktop shell, but it is not binding until a focused integration/package proof confirms it fits the requirements.** The shell owns desktop lifecycle and approved native integration; it does not own Hive's authoritative agent, provider, persistence, or authorization rules.

The same React Workspace should be designed for reuse in more than one supported host surface. A .NET business application may host an appropriate WebView2-based Workspace surface, while a standalone Hive desktop may use a Tauri shell. A Tauri window itself is not a WinForms control and should not be assumed to embed directly inside a .NET Form.

The proof must settle packaging, process lifecycle, frontend-to-runtime communication, local security, development/build workflow, accessibility, localization, and the supported integration path for a .NET host.

### 3.4 Cross-language integration

C# is permitted where it is needed to integrate with an existing .NET business application or to retain a narrowly scoped platform adapter. It must not become a second implementation of Hive's core.

Prefer an explicit, versioned command/event API as the primary cross-language contract. Use a local IPC transport where the runtime is independently hosted. A C ABI/DLL can be considered for a specific native-library integration need, but should not be the only public architecture: Rust classes and structs are not directly interchangeable with C# classes, and native ABI, memory ownership, callbacks, cancellation, and lifecycle need explicit handling.

The internal Rust API, external runtime protocol, and UI presentation contracts are separate layers. Public contracts must not leak Rust framework types, database types, private host types, or vendor-specific orchestration objects.

## 4. Proposed logical architecture

```text
Standalone Hive desktop                       Existing business application
React + TypeScript Workspace                  Native application UI / business logic
          |                                              |
Standalone shell / client                    Thin host adapter + embedded Workspace
          |                                              |
          +---------------- versioned runtime API -------+
                                 |
                       Rust Hive Runtime
                                 |
       +-------------------------+---------------------------+
       |             |           |             |             |
    Providers      Agents    Coordination     Tools       Governance
       |             |           |             |             |
       +-------------------------+---------------------------+
                                 |
                   Hive-owned durable state
                                 |
                 Provider APIs / host capabilities
```

This is a logical ownership model, not a mandate for multiple services. The first implementation should use cohesive Rust modules/crates within a small number of processes. Split deployment boundaries only when isolation, reliability, security, or operational requirements justify them.

The runtime must be independently testable and runnable without the Workspace. A standalone shell and a .NET host adapter must consume the same semantic runtime contracts instead of implementing parallel Agent or authorization behavior.

## 5. Runtime and domain boundaries

The following are candidate module/crate responsibilities. They are not a requirement to create one crate for every noun on day one; create separate crates when they establish a real ownership, dependency, or testing boundary.

| Candidate module | Responsibility |
|---|---|
| `hive-domain` | Stable domain concepts, identity, invariants, lifecycle semantics, and policy inputs. No UI or provider framework dependency. |
| `hive-runtime` | Runtime lifecycle, command dispatch, execution supervision, cancellation, scheduling boundaries, and recovery coordination. |
| `hive-providers` | Provider/account/target configuration, provider adapters, model discovery, capability evidence, streaming, and provider error normalization. |
| `hive-agents` | Agent definitions, specialization, instances, memory/experience boundaries, execution context, and delegation requests. |
| `hive-coordination` | Multi-Agent assignment, dependencies, parallel work, result evaluation, conflict handling, synthesis, and bounded stopping rules. |
| `hive-tools` | Tool registry, input/output contracts, capability checks, execution, limits, and result provenance. |
| `hive-hosts` | Neutral host-context and host-capability contracts plus adapter interfaces. Host-specific adapters remain isolated. |
| `hive-storage` | Versioned durable state, transactions, migrations, checkpoints, and recovery support. |
| `hive-api` | Versioned commands, events, DTOs, streaming envelopes, and compatibility rules for external clients. |
| `hive-app` | Executable/runtime composition, configuration bootstrap, local transport, process lifecycle, and deployment wiring. |

Keep the dependency direction deliberate. Domain contracts should not depend on concrete providers, database drivers, UI frameworks, or host adapters. A module can be introduced later if a simpler in-crate boundary is sufficient. Avoid both a monolith with uncontrolled cross-module access and a premature distributed system.

## 6. Provider platform

Provider support is a first-class, replaceable subsystem. Preserve the semantic distinction between:

- **Provider:** the vendor, service, local inference server, or protocol adapter.
- **Provider account:** the configured connection, endpoint, and credential reference.
- **Execution target:** the selectable model/deployment plus the relevant account and endpoint context.
- **Model capability evidence:** observed metadata such as supported input/output modalities, tool calling, structured output, streaming, context limits, and pricing where available.
- **Execution policy:** the rules that determine which targets are eligible for a particular task.

A provider adapter translates the provider's API into Hive-owned requests, responses, streaming events, capability evidence, and typed errors. Provider-specific behavior must not leak into Agent coordination or user-interface logic.

Requirements:

- Keep unsupported, supported, and unknown capability states distinct. Missing metadata is not proof of unsupported capability.
- Treat model catalogs, pricing, quotas, and other changing provider metadata as time-bound evidence with provenance/freshness, not timeless facts.
- Support bounded requests/responses, timeouts, cancellation where supported, redaction, safe error mapping, and provider degradation.
- Normalize common fields while preserving bounded provider-specific extensions as non-authoritative evidence.
- Avoid silently switching to another provider/model when policy or user selection requires a pinned target.
- Keep credentials outside model context, frontend code, logs, and exported configuration. Store references to secrets in Hive configuration and use an explicit platform-specific secret-protection adapter.
- Test provider adapters against deterministic fixtures and fake transports; tests must not require vendor-network calls.
- Adopt MCP or other external tool/provider standards through adapters where they offer real interoperability. External protocol schemas are not Hive's internal domain model.

## 7. Agent lifecycle and collaboration

An Agent must be more than a prompt attached to a model. Its definition describes identity, role, versioned configuration, execution-target policy, tools/capabilities, memory and experience policy, delegation permissions, limits, and approval requirements.

Keep these concepts distinct:

1. **Agent definition** — the configured role and policy.
2. **Agent instance/session** — a persistent identity in a scope or application context.
3. **Execution/run** — a specific bounded attempt to achieve an objective.
4. **Memory and experience** — durable, attributable information that can inform later work under explicit retrieval and sharing rules.
5. **Specialization/lineage** — relationships among Agent definitions and the configuration inheritance/rebase semantics that are deliberately supported.

A governing Hive/Manager can coordinate a goal, find or create a suitable specialist when policy permits, delegate bounded tasks, compare results and evidence, request critique or verification, and synthesize an outcome. The platform must be able to use simple direct model interaction or one Agent when collective reasoning adds no value.

Collaboration requirements:

- Agent-to-Agent requests and results are typed, attributable, scoped, and bounded by budgets, timeouts, cancellation, and concurrency limits.
- An Agent does not gain access to another Agent's private memory, credentials, tools, or privileges merely because it can send a message or share lineage.
- Shared task context is distinct from private Agent experience and from reusable platform Knowledge/Skills.
- Preserve the distinction between actual outcomes, model-generated claims, simulated evidence, proposals, and independently verified results.
- Agent output proposes work; runtime policy and code enforce authorization.
- Persistent runs need explicit state transitions, checkpoints where appropriate, idempotency/reconciliation rules, and terminal outcomes. A restart must not invent success when an operation's outcome is unknown.
- Frameworks can be adopted behind an adapter only if they satisfy Hive's lifecycle, persistence, observability, security, and extensibility requirements. Do not port the current orchestration framework mechanically or replace it with custom code without evaluating the actual requirements.

## 8. Host integration: declared context and bounded capability

Host integration should become a defining Hive capability. The runtime must understand what the user is working on without taking ownership of the host application's business data or depending on private visual-control details.

The preferred path is explicit host integration. A host adapter reports versioned events such as application connected, screen activated, context changed, screen deactivated, and application disconnected. It provides stable context references and exposes the limited read/actions that the host is prepared to offer.

A neutral host contract should cover:

- Application and integration identity.
- Screen/surface type and instance identity.
- Business-context reference (for example, a sales invoice ID) and any authorized, bounded contextual data.
- Context lifecycle and revision/version information so stale UI and proposals can be rejected.
- Declared read surfaces and bounded operation capabilities.
- Action request/result semantics, including validation failures, cancellation, and uncertain outcomes.
- Capability ownership, disposal, reconnect, and failure behavior.

Example intended flow:

1. A business application activates a Sales Invoice screen and reports its stable context reference through its host adapter.
2. Hive checks the declared context and permitted capabilities, then resolves the relevant configured specialist.
3. The runtime creates or resumes the appropriate Agent session and execution according to policy; opening a screen alone does not authorize a consequential action or create unrelated Hive membership.
4. Workspace displays the active invoice context, assigned specialist, execution state, results, and approval requests.
5. If the context closes or changes, Hive detaches or updates that context predictably. Persistent Agent memory and unrelated executions are not deleted accidentally.
6. A proposed write is checked against current authorization, the exact target/context revision, and any approval requirement before the host adapter executes it.

The host remains authoritative for its business records, domain validation, and native application rules. Hive may persist context references, approved snapshots, proposals, receipts, and audit information needed for its own work, but it must not copy the entire host domain into its own database by default.

Host UI discovery is a compatibility fallback, not the foundational contract. When a host cannot expose an explicit API, an isolated adapter may perform bounded read-oriented discovery. Such discovery must be cycle-safe, cancellation-aware, limited, and incapable of granting action authority. Do not expose raw control handles, arbitrary reflection/invocation, SQL, credentials, or private host types through neutral Hive contracts.

## 9. Workspace and host experience

Workspace is the common operational surface across standalone Hive and supported embedded hosts. It should display:

- Current application/screen context and the source of that context.
- The relevant Agent or specialist and why it was selected.
- Conversation, objectives, WorkItems/jobs, progress, execution status, and terminal results.
- Proposed actions, the exact target and affected data, required approvals, and verified outcomes.
- Errors and recovery actions that are safe and meaningful for the user.
- Provider/model information when useful without making provider internals the user experience.

The Workspace is a presentation/control surface. It is not the authority for identity, persistence, scheduling, target selection, authorization, or business actions.

The same user experience should support standalone workflows that do not have a host business application open. Host context enhances the Workspace; it must not become a requirement for all Hive usage.

Keep frontend data flow explicit. React sends versioned commands through a narrow host/client bridge and renders typed results/events. It must not connect directly to databases, provider credentials, secret storage, or arbitrary host controls. Event subscriptions and asynchronous requests must have bounded ownership, cancellation/disposal rules, and protection against stale events updating replaced or closed screens.

## 10. Persistence, configuration, and recovery

Hive-owned configuration and execution state belong to the Rust runtime's persistence boundary, not to individual UI shells or host adapters.

Define durable schemas and migrations for the resources that Hive actually owns, including provider configuration and secret references, Agent definitions and lineage, execution targets/policies, Agent instances, runs/checkpoints, context references, approvals, results/artifacts, and audit events as appropriate.

Requirements:

- Assign a single authoritative owner to each persisted state transition.
- Make transaction and consistency boundaries explicit, especially when host operations are involved.
- Record whether an operation was proposed, approved, attempted, succeeded, failed, cancelled, or left with an unknown outcome.
- Preserve provenance, scope, timestamps/versioning, and the identities needed to correlate execution, context, authorization, and results.
- Support safe migration, backup, restore, and schema evolution; never silently reinterpret or discard durable user state.
- Keep secrets out of general configuration exports, audit text, logs, and model-visible context. Separate portable configuration from credentials, live executions, host bindings, and private Agent experience.
- Enforce resource limits and bounded storage/cleanup without deleting live or legally/audit-relevant records accidentally.

**Database choice is an explicit design gate.** Select the first production backend based on the intended deployment model and operational needs. Do not mechanically recreate the existing SQL Server and Embedded parity merely because both exist in the current implementation. Equally, do not add a second backend without a concrete need and a tested contract. Versioned configuration export/import is a separate portability contract, not a substitute for safe database migration.

For platform credential protection, define a secret-store interface with a Windows implementation when needed and replaceable implementations for other deployment targets. The platform-specific adapter owns OS credential API details; no credentials cross into model context or frontend state.

## 11. Runtime API, events, and execution transport

Define a stable, versioned runtime contract before the Workspace or host adapters depend on implementation types.

The API should support:

- Commands with stable identifiers, typed payloads, authorization context, and correlation IDs.
- Events that report durable state changes and execution progress.
- Streaming content and status without pretending to have percentage progress when the runtime has no such evidence.
- Cancellation requests and explicit terminal outcomes.
- Subscription lifetime, replay/resynchronization where supported, and stale-event handling.
- Version negotiation and safe failure for unsupported versions or commands.
- Structured, safe errors with stable codes/categories and non-sensitive diagnostic context.
- Request-size, response-size, concurrency, and resource limits.

Use a transport adapter rather than coupling the domain to HTTP, named pipes, WebView messages, or a native C ABI. A local IPC transport is the leading candidate for a separately hosted local runtime; its authentication, endpoint discovery, permissions, process startup, and cleanup require explicit design. A UI shell may bridge its allowed commands to that transport. A .NET adapter must use the same domain semantics rather than creating a separate authorization or execution engine.

The first release may run the runtime in-process or as one local runtime process, but that decision must be proven against the intended standalone and embedded-host lifecycle. Do not break the architecture into microservices by default. Preserve the option to host the runtime independently later.

## 12. Security and governance invariants

These are foundational requirements, not a later security phase:

- Authorization is enforced in executable runtime/host code, not by prompt wording, UI visibility, hidden controls, model output, or Agent claims.
- The runtime verifies permission and approval requirements immediately before consequential operations, not only when a proposal is first generated.
- Approval applies to a specific proposed action, target, relevant context revision, and material change; stale proposals must be rejected or reviewed again.
- The host keeps authority over its own data, identity, validation, and business transactions. Hive receives only declared capabilities.
- No model receives credentials, unrestricted SQL, private host object handles where a semantic contract is sufficient, or arbitrary method invocation.
- Tool and host capabilities are allowlisted, scoped, auditable, and subject to timeout, cancellation, and resource limits.
- Secrets and sensitive host fields are redacted before logs, diagnostics, events, or model context.
- Agent collaboration, delegation, and memory sharing are policy-governed and preserve provenance.
- Plugin/provider/host failures are treated as untrusted boundary failures and cannot bypass authorization.
- Packaging, dependency selection, updates, and local runtime communication are included in the threat model.

## 13. Implementation sequence — proposed stages, not roadmap identifiers

These stages define an order for planning. They do not create numbered Hive roadmap phases, Active Work, or implementation authorization.

### Stage 0 — Adopt or reject the target architecture

Resolve the conflict between this proposal and the current canonical architecture. Decide:

- Whether Hive adopts a Rust-first replacement rather than the existing C#/.NET implementation path.
- Whether the new implementation lives in a dedicated repository or in an explicitly isolated subtree during transition.
- Which existing behavior is required for initial parity, which is deliberately deferred, and which is no longer a goal.
- The first supported deployment model and database choice.
- The standalone shell candidate and the supported way to reuse Workspace in a .NET host.
- The runtime process/transport boundary and versioning strategy.
- How the existing React/WebView2 migration proposal is dispositioned if its premise conflicts with the adopted target.

Before implementation begins, update the canonical architecture and roadmap through the normal workflow. Retain this proposal as history or explicitly mark its final disposition. Do not leave two incompatible plans both presented as the adopted path.

### Stage 1 — Runtime skeleton and durable contract

Create the smallest Rust workspace and executable/runtime boundary. Establish domain identifiers, typed errors, structured configuration, lifecycle, logging/redaction, persistence/migrations, command/event envelopes, versioning rules, and deterministic tests. Prove that the runtime can start, perform a simple command, persist state, restart, and return a truthful result without a UI.

**Gate:** no frontend dependency in the domain/runtime; explicit dependency direction; durable state survives the defined restart scenario; protocol and failure behavior are tested.

### Stage 2 — Provider foundation

Implement the provider/account/execution-target concepts, one production provider adapter, model/capability discovery, a fake provider for deterministic tests, credential integration, streaming where supported, cancellation and typed failure handling.

**Gate:** adding a second provider does not require changes to Agent/coordination domain logic; missing capabilities remain Unknown; credentials are absent from model context and diagnostics.

### Stage 3 — Base Agent execution

Implement versioned Agent definitions, execution runs, objective/task input, runtime state transitions, tool authorization boundaries, result provenance, cancellation, and durable recovery behavior.

**Gate:** direct model use and a single Agent both work through the same runtime contracts; tests cover provider failure, interruption, cancellation, invalid transitions, and recovery.

### Stage 4 — Governed collaboration

Add specialist resolution/assignment, Manager or governing-Hive coordination, bounded delegation, independent work, result comparison, challenge/verification where justified, and explicit memory/evidence sharing.

**Gate:** each Agent's identity and state remain distinct; delegation is attributable and bounded; unauthorized tools, private memory, or consequential actions cannot be reached through another Agent.

### Stage 5 — Host context and capability contracts

Define the generic host contract and provide one concrete adapter for a real host surface. Include stable application/screen/context identity, lifecycle, safe context projection, authorized read operations, bounded action requests, and stale-context handling.

**Gate:** context activation/deactivation is deterministic; closing a form does not destroy unrelated persistent Agent state; discovering a screen does not grant write authority; the host remains the owner of business data.

### Stage 6 — End-to-end business-context vertical slice

Demonstrate one real workflow, initially a Sales Invoice scenario where a host can expose suitable context:

1. Activate an invoice and publish its context reference.
2. Resolve and initialize or resume the configured specialist.
3. Show the context and specialist in Workspace.
4. Ask the specialist to analyze the authorized invoice context.
5. Produce a typed proposal with provenance.
6. Apply authorization and approval requirements before a consequential write.
7. Capture the host's result and retain a durable, auditable execution outcome.
8. Close/reopen the invoice and restart the runtime to prove lifecycle and recovery semantics.

**Gate:** the full path is proven, including a denied action, a stale context/proposal, a host failure, and a restart/recovery case. Do not claim a production host-integration capability based only on a mock UI.

### Stage 7 — Workspace and desktop packaging

Implement the first coherent React Workspace on the established client API. Complete the chosen standalone shell proof, package frontend assets locally, support the chosen runtime lifecycle, add language/RTL foundations, accessibility and desktop error handling, and provide the supported .NET embedding/host-adapter path.

**Gate:** normal use needs no development server or internet-hosted frontend; unsupported bridge requests fail closed; language switching and RTL are tested; shell shutdown/reconnect and stale-event behavior are verified.

### Stage 8 — Production hardening and portability

Complete migration/backup/restore strategy, packaging and upgrade behavior, dependency/security review, operational diagnostics, resource limits, concurrency/failure testing, documented supported host integration, and an explicit compatibility/export policy for any data worth migrating from the existing Hive implementation.

**Gate:** the supported deployment can be installed, upgraded, backed up, restored, diagnosed, and recovered under documented failure scenarios. Developer verification is recorded as actual results; inspection alone is not reported as a pass.

## 14. Verification strategy

Use deterministic tests as the primary basis for correctness. Provider and host boundary tests must use fixtures or fakes unless an explicitly authorized integration test is intended.

Coverage should include:

- Domain invariants, state transitions, and typed error mapping.
- Provider adapter contracts, capability normalization, timeouts, cancellation, streaming, and redaction.
- Agent lifecycle, concurrent delegation, authorization, memory isolation, budgets, and failure recovery.
- Persistence transactions, schema migration, crash/restart recovery, backup/restore, and unknown host-operation outcomes.
- Host identity and context revisions, form closure/replacement, capability scoping, stale proposals, and denied operations.
- Protocol versioning, invalid or oversized messages, unknown commands, reconnect/resynchronization, event disposal, and concurrency.
- Workspace/client contract tests, localization/RTL, accessibility, packaging, and lifecycle.
- End-to-end scenarios proving the full context-to-specialist-to-governed-action workflow.

Tests must distinguish what was compiled, automated-tested, integration-tested, manually verified, and not verified. No plan or architecture document may claim verification that has not actually occurred.

## 15. Major decisions to settle before implementation

1. **Adoption boundary:** Is this a full replacement of the C# platform, a fresh implementation that temporarily coexists, or only a future alternative? The proposal assumes a Rust-first replacement if adopted.
2. **Repository strategy:** A dedicated repository is the cleanest default for an incompatible platform rewrite. Keeping the new workspace in the current repository is acceptable only with an explicit isolation/transition rule.
3. **Runtime hosting:** One independently runnable local Rust process is the leading deployment candidate for multiple shell/host clients, but must be evaluated against startup, local IPC security, resource use, and solo-developer complexity. Do not split internal modules into separate services.
4. **Desktop shell:** Validate Tauri for the standalone app and React/WebView reuse; do not assume a Tauri UI can be embedded directly in .NET.
5. **Initial persistence:** Pick one durable backend appropriate to the first deployment mode. Add more only from demonstrated requirements.
6. **Provider/framework dependencies:** Evaluate mature crates against the contract. Keep public Hive semantics independent from any one framework.
7. **Legacy behavior and data:** Decide what existing capabilities and data must be imported or preserved rather than accidentally inheriting every current implementation detail.
8. **External protocols:** Adopt MCP, A2A, or other protocols through explicit adapters where they provide real value; do not make external protocol schemas Hive's internal domain model.

## 16. Acceptance criteria for adopting this plan

This proposal is ready to be promoted into the canonical architecture only when:

- The product thesis and system boundaries are accepted.
- The Rust-first replacement decision is explicit.
- Repository and runtime deployment boundaries are chosen.
- Provider, Agent, collaboration, host integration, persistence, API, and governance contracts have clear owners.
- The first vertical slice and its failure/security acceptance cases are defined.
- The existing React/WebView2 plan and canonical .NET architecture have a documented disposition.
- The roadmap is updated with an ordered implementation path and the normal workflow records explicit authorization before implementation.
- No planned behavior is misrepresented as implemented or verified.

## 17. Current repository relationship

The current repository remains authoritative for its present implementation and workflow. At the date of this proposal:

- `docs/architecture.md` describes Hive as a C# / .NET 10 platform.
- `docs/plan/React-WebView2-UI-Migration.md` proposes a UI migration that preserves the current backend.
- `docs/roadmap.md` remains the ordered plan for the current implementation.
- `docs/Hive_Active_Work.md` states that no task is active.

This document records a different strategic direction for evaluation. It does not change those facts, override their authority, authorize a new project/phase, or imply that a Rust implementation exists. If this direction is adopted, the canonical architecture, roadmap, related plans, current status, and active authorization must be reconciled explicitly before implementation.

## References

- `AGENTS.md`
- `docs/architecture.md`
- `docs/roadmap.md`
- `docs/Hive_Current_Status.md`
- `docs/Hive_Active_Work.md`
- `docs/plan/React-WebView2-UI-Migration.md`
- `docs/plan/Phase1/1.20.md`
- `docs/plan/Phase1/1.21.md`
- `docs/architecture/agents-and-hives.md`
- `docs/architecture/execution-and-persistence.md`
- `docs/architecture/v1-host-and-management.md`
- `docs/architecture/v1-business-app-integration.md`
