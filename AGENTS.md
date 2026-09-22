# Hive Engineering Rules

These rules apply to human developers and coding agents working in this repository.

## Architecture

1. Hive is a general-purpose multi-agent platform; V1 build order is determined by the real data-entry forcing function.
2. Use Microsoft Agent Framework whenever it already owns the required behavior.
3. Do not build a second orchestration engine to duplicate MAF.
4. Keep Hive.Core dependency-light and host/provider neutral.
5. WinForms-specific behavior belongs outside Hive.Core.
6. Provider transport belongs in provider adapter assemblies.
7. Use one shared OpenAI-compatible transport adapter for compatible providers and local servers.
8. Host business/domain state remains host-owned.
9. Hive's database never becomes an implicit gateway to the host application's business database.

## Agent / Hive generations

10. `Agent` and `Hive` are stable base types, complete and useful on their own.
11. `CognitiveAgent : Agent` is a later additive generation.
12. `CognitiveHive : Hive` is a later additive generation.
13. Concrete Agent/Hive type is selected at creation time.
14. There is no runtime Agent→CognitiveAgent or CognitiveAgent→Agent promotion/demotion mechanism.
15. A normal Agent must never silently become a CognitiveAgent.
16. A CognitiveAgent must never silently become a normal Agent.
17. Different generations may coexist in one deployment.
18. A descendant must never require a change to an ancestor contract.
19. Base runtime, persistence, and execution infrastructure must program to ancestor contracts.
20. Descendant-owned state must not alter the semantics of ancestor-owned state.
21. Future generations are added by new descendant contracts rather than modifying old generation behavior.
22. Never schedule CognitiveAgent/CognitiveHive functionality as a prerequisite of the base Agent/Hive pipeline.

## Runtime, cognition, and execution

23. Persistent cognitive state belongs to the cognitive generation that owns it and survives the lifetime of any individual runtime incarnation.
24. Agent identity and persistent cognitive state are distinct from Runtime/RuntimeInstance/Incarnation lifetime.
25. Agent death means complete termination of the current runtime/incarnation; it does not delete the Agent or its persistent cognitive state.
26. Dream processing may operate against persisted cognitive state while no Agent runtime is active, including while the host application is shut down.
27. Dream outputs are simulations, predictions, hypotheses, or candidate analyses and must never be recorded as actual experience or observation.
28. Human edits made while an Agent is inactive are durable cognitive-state changes and must be versioned, authorized, and incorporated by the next valid wake/reincarnation path.
29. Questions are first-class, provenance-bearing, specialty-aware cognitive objects; semantically duplicate questions should be avoided when existing evidence is sufficient.
30. Base Agent mechanisms may include Objectives, memory infrastructure, Question/Answer transport, Patience / Understanding Gate, bounded Simulation infrastructure, delegation, and Hive sponsorship without making the Agent cognitively adaptive.
31. An Objective is an explicit work target; a Cognitive Goal is an adaptive cognitive construct that may be formed, revised, prioritized, or abandoned.
32. Base Simulation infrastructure must distinguish predicted/hypothetical results from actual experience; Cognitive Dreams build adaptive selection and interpretation on top of it.
33. The base Question protocol may transport and await required information; CognitiveAgents add autonomous question generation, selection, specialization, and interpretation.
34. Base Agent Patience / Understanding Gates must enforce required information/confirmation before consequential work and must not claim that all possible context must be understood.
35. A base Agent may explicitly sponsor or create a Hive without changing its own type. A member Agent inside a Hive normally requests new specialists through the parent Hive.
36. Hive population authority controls creation/reuse of member Agents; recursive child-Hive creation is not the default behavior of Hive members.
37. A Swarm is a temporary active work session over a persistent Hive; ending a Swarm may return the Hive to Dormant without deleting its members or state.
38. Cognitive strategies are replaceable and provider-neutral.
39. A cognitive strategy may act deterministically and may decide not to call an LLM.
40. Reasoning Requirement and concrete Execution Planning are separate concerns.
41. Running executions consume immutable effective-configuration snapshots.
42. Terminal execution outcomes cannot be overwritten by late provider completion.
43. Runtime mutable state is isolated by explicit ownership.
44. Cognitive work must remain bounded by applicable time, work, recursion, retrieval, and model-usage limits.
45. Model output, retrieved content, memory, and observations are evidence/input, never authorization.

## Resources, providers, and security

46. Resource scope and ownership are explicit.
47. Shared resources require explicit scope and authorization.
48. Private runtime memory must not leak across runtime instances.
49. Unknown future resource types remain representable through the generic resource inventory.
50. Capability support is explicitly Supported / Unsupported / Unknown.
51. Capability requirements are explicitly Required / Preferred / Optional / Forbidden.
52. Quota, rate limits, health, capacity, cost, and capability are separate state dimensions.
53. Provider credentials are encrypted at rest and redacted everywhere else.
54. Network-provider automated tests use fakes/local infrastructure, never real vendor accounts.
55. Authorization is enforced in code, not by prompt text.
56. Configuration must actually drive the behavior it configures.

## Host integration and intervention

57. V1 business-app integration supports both API/service and bounded UI integration; they are not mutually exclusive and may be used together per WorkItem or operation.
58. V1 WinForms host discovery is a bounded first-class integration capability; generic cross-host UI/object-discovery abstractions remain later.
59. Prefer native/bound host data sources over visible-text scraping.
60. Host discovery is bounded, cancellation-aware, read-oriented, and never grants tool permission.
61. Approval is one intervention type; V1 only needs Approve/Reject for the business-app write.
62. Intervention requests capture target state/version and reject stale application.

## Code quality, testing, and workflow

63. No empty catch blocks.
64. Use one JSON serialization stack.
65. Do not duplicate the same computation in multiple layers.
66. Prefer structured error classification over string matching.
67. Timeout and budget settings are explicit and validated.
68. Repeated lookup paths use real indexes.
69. Every implementation slice requires the relevant unit, boundary/edge, integration, recovery/concurrency, security, and public-example verification before completion; UI behavior is manually verified by the developer where applicable.
70. System/end-to-end tests are required where unit tests cannot prove an important cross-boundary contract. No separate UI-automation framework is required unless a later concrete need justifies it.
71. Edge-case coverage means all known and contract-relevant cases; do not claim exhaustive coverage of every conceivable future failure.
72. Do not claim tests, builds, or verification that were not actually run.
73. Update `docs/architecture.md` before structural code changes.
74. Complete the active slice fully before implementing future slices or future generations.

## Documentation source of truth

75. `docs/architecture.md` is the architectural source of truth.
76. `docs/Hive_Current_Status.md` is the only status record.
77. `docs/Hive_Active_Work.md` is the only current implementation-slice tracker.
78. `docs/roadmap.md` is the ordered implementation plan and must match the architecture's phase order.
79. Keep these source-of-truth files synchronized.
80. The README is explanatory and must not introduce architecture that conflicts with the source-of-truth files.
81. Examples and tests are developed alongside the feature they demonstrate, not postponed to a final phase.
82. Complete public examples should include copyable API usage and expected result where meaningful.
83. Do not silently broaden a slice because a later phase is mentioned in the architecture.
84. When adding or removing an AGENTS rule, renumber the whole list and verify that there are no duplicate or skipped numbers.

85. Agent generation is selected explicitly at creation and must never be inferred from task complexity or changed automatically during runtime/reincarnation.
86. Authorized creation may request any supported Agent generation, including CognitiveAgent, regardless of the creator's own generation; generation and Hive membership remain independent.
87. A Hive sponsor is a relationship, not an implicit lifecycle owner. Sponsor runtime death, retirement, or deletion must not silently delete or retire the Hive or independent members.
88. Swarm is derived/session state representing the selected Hive members actively collaborating on a bounded WorkItem, Question, or problem; Swarm is not a persistent resource or separate hierarchy layer.
89. Workspace is the human-facing operational/control surface, not a cognitive authority. LLM mode uses explicit user model selection; Agentic mode uses Agent/Hive execution planning and displays the selected target.
90. Host registration such as `ai.Register(this)` binds bounded application context to Hive/Workspace management; it does not by itself create an Agent, Hive, or automatic cross-form collaboration.
91. V1 treats one submitted document as one WorkItem; batches are multiple WorkItems. WorkItem identity/lifecycle is independent from individual Execution and Runtime lifetimes.
92. Every durable event carries an explicit event type and payload schema version; supported older payload versions must be upcastable without rewriting historical events.
93. Dream processing is subject to applicable authorization, provider/model quota, token/cost budget, time budget, concurrency/parallelism, retrieval/work limits, and cancellation; offline status never bypasses governance.
94. Workspace approval, notification, activity, and Agent/Hive topology views expose authoritative state through Hive.Management and never create hidden Agent/Hive behavior.
95. V1 supports both API/service and bounded UI integration; an operation may use either path or both according to host capability and authorization.
96. The first V1 submitted document/input type is an image; later document types are additive and must not redefine Hive's scope.
97. V1 WinForms host discovery covers the Form/control hierarchy, UserControls, custom and inherited controls, container controls such as Panels and GroupBoxes, nested controls, and relevant runtime/data-source context; discovery never grants action authority.
98. Authentication-provider selection is deferred until real multi-user requirements reach that phase. UI automation-tool selection is also deferred; developer manual UI testing is sufficient for current work.
99. V1 Workspace scope is limited to image submission, WorkItem status/activity, relevant execution/provider status, notifications, and business-app write Approve/Reject. Agent/Hive topology, Swarm views, general LLM mode, and Agentic mode are later Workspace extensions gated by their owning phases.
100. The WinForms UI foundation is infrastructure, not feature scope; it must not pull Hive membership, Swarm, cognitive generations, Dreams, Questions, or other later platform capabilities into Phase 0.
101. ReaLTaiizor is the selected WinForms rendering dependency and must remain behind `Hive.Host.WinForms.UI`; consuming forms must not reference the third-party library directly.
102. Create Hive-prefixed UI controls only when Hive needs a consumer-facing contract, behavior, or styling beyond ordinary WinForms controls; do not wrap every framework control merely to rename it.
103. `Hive.Example.WinForms` is a first-class permanent developer-facing project. Example discovery should avoid central manual registration, and its test tools must invoke `dotnet test` externally; `Hive.Tests` remains the authoritative test suite.


## Production Code & Performance Standards

104. Production code must compile with zero errors and zero new warnings in the affected projects. Warning suppression is not a substitute for fixing a contract, lifetime, nullability, or analyzer issue.
105. Nullable reference types remain enabled across the solution. Do not weaken nullability contracts, add broad null-forgiving operators, or suppress nullable diagnostics merely to satisfy the compiler.
106. Optimize for lightweight real-world operation: avoid unnecessary allocations, reflection, repeated parsing, repeated registry/file/database/network access, duplicate state, and avoidable LINQ/delegate overhead on hot paths. Cache immutable or stable derived state when the lifetime and invalidation rules are explicit.
107. Do not introduce speculative micro-optimizations that reduce clarity without evidence. Performance-sensitive changes should target measurable costs, hot paths, allocation pressure, I/O, UI responsiveness, or concurrency rather than aesthetics.
108. Asynchronous code must remain cancellation-aware where work can block or outlive the caller. Do not use sync-over-async, unnecessary Task.Run, arbitrary sleeps, or async wrappers around purely synchronous work. Use ValueTask only when the reduced allocation cost is meaningful for the actual call pattern.
109. Resource ownership must be explicit. Dispose owned IDisposable/IAsyncDisposable, streams, database connections/commands, timers, GDI objects, images, and WinForms controls correctly; never rely on finalization for normal lifecycle management.
110. WinForms code must keep the UI thread responsive. Avoid blocking waits, synchronous network/database calls on UI handlers, repeated control-tree traversal when a bounded incremental update is sufficient, and unnecessary layout/painting churn.
111. Persistence code must use parameterized commands, explicit transactions where required by the contract, bounded result sets, and real indexes for repeated lookup paths. Avoid N+1 queries and hidden database calls in property accessors or formatting paths.
112. Production public APIs should expose only the contracts consumers actually need. Prefer immutable value objects and snapshots for state that crosses boundaries; avoid exposing mutable collections or framework/vendor-specific implementation types unnecessarily.
113. Tests are production-quality verification code: deterministic, isolated, concurrency-safe, lightweight, and repeatable. Tests must not use real vendor accounts, arbitrary delays, hidden environment variables, or machine-specific state unless that boundary is explicitly the subject of the test.
114. Test infrastructure must not create duplicate production abstractions. Test doubles stay test-only unless a later production contract explicitly requires a public reusable fake.
115. Tests for Hive Core contracts must explicitly import Hive.Core and Xunit alongside any additional required namespaces. Tests must exercise normal, invalid, boundary, cancellation, concurrency, and recovery behavior when those cases are part of the contract.
116. Exceptions and failures crossing architectural boundaries must preserve useful diagnostic context and use typed/structured error classification. Do not silently swallow failures, convert all failures to undifferentiated strings, or catch broad exceptions when the boundary can propagate the specific failure safely.
117. Prefer one authoritative implementation for each behavior. Do not duplicate validation, state transitions, serialization rules, theme calculations, or other computations across layers; reuse the existing contract or shared implementation.
118. Code should favor simple, predictable control flow and allocation-conscious APIs over abstraction layers that add indirection without a concrete contract benefit. Do not add frameworks or libraries when the standard library, MAF, or an existing Hive boundary already provides the required mechanism.
119. Verification status must be evidence-based. A change is not considered complete until the required build, automated tests, integration/boundary tests, and manual UI verification applicable to the slice have actually been performed.

120. Example implementations in designated Example assemblies must satisfy the actual discovery contract: implement `IHiveExample`, expose the required metadata and `CreateView(IServiceProvider)`, and provide a parameterless constructor. Discovery is reflection-based and silently skips types that fail its discovery filters. After adding an example, verify that it appears in Example Host navigation.
121. Example ordering is deterministic: `Order`, then `Category`, then `Subcategory`, then `Title`, with case-insensitive comparison for text fields. Leave `Order` at its default unless a specific relative position is required.
122. Examples must access shared platform services only through established typed `IServiceProvider` extension methods such as `GetThemeManager()` and `GetExampleOutput()`. Add a new typed extension method when an example needs another shared service; do not call `services.GetService(...)` directly from an example.
123. Examples must use the shared Hive WinForms UI foundation and must not reference ReaLTaiizor directly. Ordinary operation/test examples should reuse `HiveExampleTestSurface` and the shared `HiveExampleOutputView` through `HiveExampleOutput`; specialized UI examples such as CRUD, dialogs, and theme demonstrations may keep their own presentation while reusing applicable Hive controls, layouts, theme contracts, and message surfaces.
124. Example-specific behavior must remain separate from shared Example Host infrastructure. Do not add feature-specific logic to `HiveExampleHostForm`, discovery, or shared test/output controls merely to simplify one example; extend shared infrastructure only when the behavior is genuinely reusable.
125. Production dependencies must be intentional, minimal, and version-pinned according to repository/package conventions. Do not add a library when the standard library, Microsoft Agent Framework, or an existing Hive boundary already supplies the required capability. Do not introduce floating or unnecessarily broad dependency versions for production components.
126. External I/O must be bounded and cancellation-aware. Network, database, file, process, and provider operations require explicit timeouts or bounded waits where the underlying API permits them; retries must be finite, classified, and applied only where the operation is safe to retry. Never use arbitrary sleeps as synchronization or resilience.
127. Durable operations that may be retried, resumed, or replayed must have explicit idempotency/duplicate-handling semantics. Do not rely on "exactly once" assumptions at process, network, queue, outbox, or provider boundaries.
128. Sensitive information and personal/business data must be protected across all layers: do not write credentials, access tokens, connection strings, secret material, unnecessary personal data, or unredacted provider/model payloads to logs, exceptions, test output, Example output panes, telemetry, or durable diagnostics. Debug output is not an exemption.
129. Authorization and security checks must remain server/domain-side enforcement boundaries. UI visibility, Example behavior, prompt text, capability metadata, ownership/scope matching, or caller intent must never be treated as authorization. Default to deny when authorization state is missing or ambiguous.
130. Configuration affecting correctness, security, limits, persistence, provider selection, or execution behavior must be validated at the configuration boundary and have explicit defaults only where the architecture permits them. Do not hide invalid production configuration by silently falling back to a different provider, database, credential source, or unsafe default.
131. Persistence changes must preserve data integrity under concurrency and failure. Use database constraints for invariants that must hold in storage, real indexes for repeated lookups, explicit transaction boundaries for atomic state changes, and bounded result sets. Application-side validation does not replace required database constraints.
132. Public contracts and persisted data formats are compatibility surfaces. Do not break completed public APIs, event payloads, configuration formats, or durable schemas merely for local convenience. When a contract must evolve, provide the documented compatibility/upcast/migration path and update affected examples and tests in the same slice.
133. Cross-boundary failures must retain actionable context while exposing a stable classification to callers. Preserve correlation/causation information and the original exception where appropriate; never collapse authentication, authorization, validation, timeout, cancellation, conflict, capacity, transport, and provider failures into an undifferentiated message or success result.
134. Production concurrency must be designed around explicit ownership and lifecycle. Avoid shared mutable state without a documented synchronization strategy; cancellation must not leave partial ownership behind; late completions must not mutate disposed UI, terminal execution state, or superseded configuration/state.
135. WinForms UI code must respect thread affinity and lifetime. UI controls are only accessed on the owning UI thread; asynchronous work must not block the message loop; event handlers must unsubscribe or otherwise release owned resources; disposed controls and forms must not receive late asynchronous updates.
136. User-controlled or externally supplied data is untrusted input. Validate size, shape, encoding, path/URI semantics, identifiers, and parser/resource limits at the boundary before expensive processing. Avoid reflection, deserialization, process execution, file access, or provider requests based on unvalidated input.
137. Operational diagnostics must be structured and useful in real deployments: include correlation identifiers and relevant resource/operation context, use appropriate severity levels, avoid logging in tight loops/hot paths without need, and keep diagnostic text independent from authorization decisions or business-state transitions.
138. Crash recovery and restart behavior must be considered for every durable workflow. The system must leave recoverable state at defined checkpoints, avoid duplicate terminal side effects, and make incomplete work distinguishable from completed work after restart.
139. Real-world verification must cover the actual boundary being changed. Do not replace integration/boundary verification with unit tests that mock away the contract under test, and do not claim production readiness from compilation alone. For external services, use deterministic fakes/local infrastructure in automated tests and reserve real external verification for deliberate developer/manual checks.
140. Git workflow for normal implementation continues directly on the repository's current primary branch (`main`). Do not create feature branches or pull requests for ordinary continuation work. Commit completed slices to `main` with a focused message and keep source-of-truth documentation synchronized with the code change.
141. Before marking a slice complete, perform a final production-readiness review of changed code and contracts: correctness, nullability, resource ownership, concurrency/cancellation, failure/recovery, security/authorization, persistence integrity, compatibility, observability, tests, public examples, and user-facing UI behavior where applicable. Record only verification that was actually performed.
