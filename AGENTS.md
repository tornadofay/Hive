# Hive Repository Operating Constitution

This file defines how AI coding agents operate in the Hive repository. It governs agent behavior; detailed architecture, roadmap, API, and product knowledge belong in the documents referenced below.

An agent must follow this constitution before changing code, tests, configuration, or source-of-truth documentation.

## 1. Repository identity and current technology

1. Hive is a general-purpose C# / .NET 10 multi-agent platform. The V1 forcing function is document/image data entry into an existing business application; that use case does not redefine Hive's long-term scope.
2. The solution is `Hive.sln`. Runtime targets are `net10.0` for platform libraries and `net10.0-windows` for WinForms projects.
3. The repository uses C# with SDK-style projects, NuGet `PackageReference`, nullable reference types, implicit usings, latest language/analyzer settings, and `TreatWarningsAsErrors=true` through `Directory.Build.props`.
4. `Hive.Tests` is the authoritative automated test project and uses xUnit.
5. Hive persistence currently uses SQL Server, with LocalDB as the supported local-development form of the same SQL Server boundary. Persistence implementation belongs to `Hive.Persistence`.
6. `Hive.Example.WinForms` is a permanent developer-facing application and visual/public-API example host, not a production test runner.
7. No CI/CD or release pipeline is currently defined in the repository. Do not claim CI verification or invent a release process. CI/CD is a later roadmap concern.
8. No separate deployment/packaging contract is currently defined. Do not introduce deployment architecture unless the active slice or an explicit request requires it.

## 2. Source-of-truth hierarchy

9. `AGENTS.md` is authoritative for AI operating behavior, scope control, verification honesty, and repository workflow.
10. `docs/architecture.md` is authoritative for architectural intent and dependency boundaries.
11. `docs/Hive_Active_Work.md` is authoritative for the exact implementation slice currently allowed.
12. `docs/roadmap.md` is authoritative for ordered implementation scope after the active slice.
13. `docs/Hive_Current_Status.md` is authoritative for recorded project status and completed verification history.
14. Source code and project files are authoritative for what is actually implemented.
15. Tests are authoritative evidence of behavior that has actually been exercised; tests do not override architectural intent.
16. README.md is explanatory/project-facing documentation and must not override the source-of-truth documents.
17. Example documents under `docs/examples` explain public usage and verification patterns; they must match the real APIs they demonstrate.
18. When documents and implementation disagree, do not silently choose an architecture. Identify the conflict, determine whether the active task is to reconcile it, and update the appropriate source-of-truth document before making a structural change.
19. Do not use previous chat history as a repository authority. The repository must be sufficient for a new agent to determine the current state.

## 3. Required workflow before implementation

20. Read this file completely.
21. Read `docs/Hive_Current_Status.md` and `docs/Hive_Active_Work.md` to establish the current checkpoint and allowed slice.
22. Read the relevant section(s) of `docs/roadmap.md`.
23. Read the relevant section(s) of `docs/architecture.md`. Read the full architecture document when the change crosses project boundaries, changes a public contract, persistence, orchestration, lifecycle, or security model.
24. Inspect the affected project files, project references, existing APIs, related implementations, tests, and examples before coding.
25. Inspect configuration/package references and existing extension points before introducing new ones.
26. Determine the smallest correct implementation slice before making edits.
27. Record internally the distinction between required work, necessary supporting work, optional improvements, and future work. Only required and necessary supporting work belongs in the active change.
28. After implementation, review the resulting diff as a code reviewer, run the verification permitted by the active task/slice, synchronize documentation, and report exactly what was changed and verified.

## 4. Scope and decision control

29. Implement only the active slice. Do not implement future roadmap slices, later generations, speculative integrations, or unrelated cleanup.
30. A necessary supporting change is allowed only when the active slice cannot be correctly implemented without it.
31. Do not refactor working unrelated code merely because another design is cleaner, newer, or fashionable.
32. Do not introduce speculative abstractions, extension points, frameworks, caches, background services, or compatibility layers without a concrete current contract requiring them.
33. Prefer the existing repository abstraction and responsibility boundary over a new abstraction. A new abstraction requires a specific contract benefit, not just code reuse.
34. If implementation reveals a broader architectural improvement, document the finding and keep it outside the active slice unless the current architecture/plan already authorizes it.
35. Do not silently change observable behavior, public contracts, persistence semantics, or lifecycle rules. Significant behavior changes require documentation and verification appropriate to the contract.
36. Human architectural decisions remain authoritative. For unresolved conflicts involving breaking API changes, database/data-model changes, security boundaries, dependency replacement, orchestration strategy, or major architecture, do not silently choose a direction. State the conflict, viable options, repository evidence, and impact.

## 5. Architecture and dependency protection

37. Preserve responsibility ownership and project dependency direction from `docs/architecture.md`.
38. `Hive.Core` remains dependency-light and host/provider neutral. WinForms, SQL client, vendor transport, and host-business dependencies do not belong there.
39. `Hive.Management` is the management/application facade. WinForms hosts must not bypass it for domain/management operations.
40. Provider transport belongs behind provider adapter boundaries. Use one shared OpenAI-compatible transport for compatible providers/local servers rather than one transport implementation per vendor.
41. Hive uses Microsoft Agent Framework wherever MAF already owns the required mechanism. Do not build a second workflow/orchestration engine.
42. Base `Agent` and `Hive` contracts remain complete on their own. Later cognitive generations are additive and are never hidden prerequisites of base functionality.
43. Agent generation is selected explicitly at creation; do not infer or perform runtime promotion/demotion.
44. Hive state remains separate from the host application's business database. Host business/domain state stays host-owned.
45. Discovery or observation never grants authorization to mutate or invoke host controls.
46. `Hive.Host.WinForms.UI` is the boundary for Hive-owned WinForms presentation infrastructure and the third-party rendering implementation selected by the architecture. Consuming projects must use Hive-owned contracts instead of directly depending on the rendering library.
47. Do not create Hive-prefixed wrappers for ordinary WinForms controls unless Hive needs a real consumer-facing behavior or contract beyond the framework control.
48. Tests must not become production architecture. Test doubles and test helpers stay in `Hive.Tests` unless a production contract explicitly requires a reusable implementation.

## 6. Dependencies and external technology

49. Inspect existing project references and package versions before adding a dependency.
50. Prefer the standard library, .NET/WinForms, MAF, or an existing Hive boundary when it already provides the required capability.
51. New production dependencies require a concrete reason, a supported version, a clear ownership boundary, and verification that they do not violate project dependency direction.
52. Do not upgrade frameworks or packages merely because newer versions exist. Upgrade only when required by the active task, security/compatibility need, or an explicitly approved modernization decision.
53. Do not replace an existing library or implementation solely for stylistic preference. Compare the actual contract, support, compatibility, and migration impact first.
54. Do not rely on experimental/deprecated APIs without checking the actual project target and package references. When an API choice affects compatibility, verify it against the repository's referenced framework/package versions.

## 7. Production coding rules

55. Preserve nullable contracts. Do not weaken nullability or add broad null-forgiving operators just to satisfy the compiler.
56. Keep public APIs minimal and intentional. Prefer immutable value objects/snapshots at architectural boundaries and avoid exposing mutable implementation state unnecessarily.
57. Preserve async behavior and cancellation semantics. Do not introduce sync-over-async, arbitrary sleeps, unnecessary `Task.Run`, or blocking waits in asynchronous paths.
58. External I/O must be bounded and cancellation-aware. Network, database, file, process, and provider work must have explicit timeout/budget semantics where supported.
59. Retries must be finite, classified, cancellation-aware, and used only for operations that are safe to retry.
60. Durable or side-effecting operations must define duplicate/idempotency behavior. Do not assume exactly-once delivery across process, network, queue, outbox, or provider boundaries.
61. Own and dispose resources deterministically: streams, connections, commands, timers, GDI objects, images, controls, and other IDisposable/IAsyncDisposable instances.
62. Cross-boundary failures must preserve useful diagnostic context and stable error classification. Do not convert materially different failures into an undifferentiated string or success result.
63. Never use empty catch blocks. Do not silently swallow failures.
64. Authorization is enforced in code at the authoritative boundary. Prompt text, UI visibility, capability metadata, ownership/scope matching, or caller intent is not authorization.
65. Treat externally supplied or user-controlled input as untrusted. Validate size, shape, encoding, identifiers, paths/URIs, parser limits, and resource limits before expensive or privileged work.
66. Configuration that affects correctness, security, limits, persistence, provider selection, or execution must be validated at its boundary. Do not silently fall back from invalid production configuration to a materially different or unsafe configuration.
67. Secrets, credentials, access tokens, and sensitive provider/business data must be redacted from logs, telemetry, test output, Example output, and durable diagnostics. Preserve useful non-secret diagnostic context.
68. Operational diagnostics should be structured and actionable, include correlation/resource context where available, and avoid unnecessary logging on hot paths.
69. Configuration must actually control the behavior it claims to control. If a setting is accepted but ignored, the implementation is incomplete.

## 8. Persistence and durable-state rules

70. Keep SQL access inside `Hive.Persistence` or the currently authorized persistence boundary. Do not place database code in `Hive.Core` or WinForms forms.
71. Use parameterized SQL, bounded queries, real indexes for repeated lookups, and database constraints for invariants that must hold under concurrency.
72. Use explicit transaction boundaries for atomic state changes. Do not depend on application-side validation alone for storage invariants.
73. Schema migrations must be explicit, ordered, repeatable, and compatible with the repository's DbUp/schema-version strategy.
74. Event payload schema versioning and database schema versioning are separate contracts. Preserve historical event readability through supported upcasting; do not rewrite historical events merely to simplify new code.
75. Durable state must remain recoverable across restart/crash at defined boundaries. Terminal side effects must not be duplicated by recovery or late completion.
76. Do not introduce a new persistence engine or database technology unless the active architecture/roadmap explicitly calls for it.

## 9. Concurrency and WinForms rules

77. Mutable runtime state must have explicit ownership and synchronization. Do not introduce shared mutable state without a defined concurrency model.
78. Late asynchronous completions must not mutate disposed controls, terminal execution state, superseded configuration, or resources that no longer belong to the operation.
79. WinForms controls are accessed on the owning UI thread. Keep UI handlers responsive and keep network/database/provider work off the UI thread when it can block.
80. Forms and controls must release event subscriptions and owned resources. Cancellation/disposal must prevent stale callbacks from updating dead UI.
81. Avoid unnecessary UI layout, painting, handle recreation, control-tree traversal, and allocation churn. Optimize measured or contract-relevant costs, not hypothetical micro-benchmarks.

## 10. Testing and verification

82. Every implementation slice must satisfy the verification gate defined by its active roadmap/plan and architecture boundary.
83. Tests should cover applicable normal, invalid, boundary, cancellation, concurrency, recovery, persistence, compatibility, and security cases.
84. `Hive.Tests` is the authoritative automated suite. The Example Host's developer test tools do not replace it and must invoke tests externally rather than embedding xUnit runner internals.
85. Automated provider/network tests use deterministic fakes or local infrastructure. Never use real vendor credentials or uncontrolled external accounts in automated tests.
86. Persistence integration tests use the repository's explicit SQL Server/LocalDB test strategy and must remain deterministic and isolated.
87. User-facing WinForms behavior requires developer/manual verification where the active slice changes UI behavior. No separate UI-automation framework is required unless a later architecture decision introduces one.
88. Publicly meaningful APIs should have a copyable example and expected result where the active feature benefits from one.
89. Verification claims are evidence-based. Distinguish clearly between inspected/reasoned, compiled, automated-tested, integration-tested, manually verified, and not verified.
90. Never claim a build, test, manual action, integration call, performance measurement, or release check that was not actually performed.

## 11. Example Host rules

91. Example implementations live in designated Example assemblies and must satisfy the actual discovery contract: `IHiveExample`, required metadata, `CreateView(IServiceProvider)`, and a parameterless constructor unless the discovery implementation is intentionally changed.
92. Example discovery is reflection-based and may silently omit types that fail its filters. After adding an example, verify that it actually appears in navigation.
93. Example ordering is deterministic: `Order`, then `Category`, then `Subcategory`, then `Title`, with case-insensitive comparison for text fields.
94. Examples access shared services through the established typed `IServiceProvider` extension methods (for example `GetThemeManager()` and `GetExampleOutput()`). Do not call `services.GetService(...)` directly from an example; add a typed extension when a new shared service is required.
95. Ordinary operation/test examples should reuse `HiveExampleTestSurface` and the shared Example output service/view. Specialized examples such as CRUD, dialogs, and theme demonstrations may use their own presentation.
96. CRUD examples/pages use `HiveCrudPage<TItem>` and related shared layout primitives when their behavior matches the generic contract. The consumer owns domain schema, validation, authorization, persistence, and the specialized editor.
97. Examples demonstrate public platform boundaries rather than private shortcuts. An Example Host success path must not depend on behavior unavailable to a real consuming host.
98. Shared Example Host infrastructure must stay feature-neutral. Do not add feature-specific behavior to discovery, the host shell, or shared test/output controls merely to simplify one example.

## 12. Documentation and source synchronization

99. Update `docs/architecture.md` before structural code changes when the architecture does not already authorize the exact change.
100. Update `docs/Hive_Active_Work.md` when the active slice, scope, dependency, or verification plan changes.
101. Update `docs/Hive_Current_Status.md` only for actual status/completion/verification changes. Do not use it as a planning scratchpad.
102. Update `docs/roadmap.md` when implementation order, slice scope, or roadmap status changes. Do not use it to record transient development notes.
103. Update README.md when user-facing project direction, public structure, or primary usage changes materially; do not duplicate detailed architecture or implementation notes there.
104. Update examples and API documentation in the same change when a completed public contract changes their usage.
105. Do not document planned behavior as implemented. Documentation is incomplete when it describes behavior that the current source does not provide.

## 13. Change review and completion gate

106. Before completion, inspect the final diff and affected files for accidental changes, stale comments, duplicated logic, dead code, generated-file mistakes, and missing disposal/unsubscription.
107. Review the change against the active requirement, architecture, dependency direction, public API compatibility, persistence, security, concurrency, cancellation, observability, performance, and failure recovery as applicable.
108. Confirm that new behavior is reachable through the intended public boundary and that configuration actually drives it.
109. Confirm that required tests/examples/documentation exist for the changed contract and that their verification status is known.
110. Separate unresolved findings into current blockers versus future work. Do not hide known limitations merely because the active slice is otherwise complete.
111. A slice is complete only when its required implementation, documentation, and verification gates have actually been satisfied.

## 14. Git and repository hygiene

112. Normal continuation work is performed directly on the repository's current primary branch: `main`. Do not create feature branches or pull requests unless explicitly instructed otherwise.
113. Before editing or committing, inspect repository status and preserve unrelated user/developer changes. Do not reset, clean, checkout, or overwrite work you did not create.
114. Keep commits focused on one completed logical change. Do not mix unrelated cleanup, formatting churn, dependency upgrades, or future-slice work into the commit.
115. Never rewrite published history, force-push, or discard commits unless explicitly instructed.
116. Do not commit secrets, local environment files, machine-specific generated output, build artifacts, test results, or other ignored/non-source artifacts unless the repository explicitly requires them.
117. Generated files must be changed through their authoritative source/generation path when one exists. Do not hand-edit generated output to conceal a source problem.

## 15. Reporting and agent behavior

118. Do not guess when repository evidence is available. Inspect the relevant file, project reference, test, or architecture section instead.
119. Do not invent APIs, package capabilities, commands, framework behavior, or verification results. Verify against the actual project references or reliable documentation when necessary.
120. Do not overwrite an established repository decision merely because an alternative seems preferable. A new direction requires evidence and the appropriate architectural decision.
121. Do not treat a successful compilation as proof of runtime correctness, concurrency safety, persistence correctness, security, UI correctness, or production readiness.
122. At completion, report: active slice, files/areas changed, architectural decisions made, tests/builds/manual verification actually performed, documentation synchronized, and known unresolved issues.
123. If verification could not be performed, say exactly why and what remains unverified.
124. Keep the repository self-describing. Do not rely on hidden context, memory, or previous conversations for essential project state.

## 16. What belongs elsewhere

125. Detailed architecture, domain models, lifecycle semantics, dependency graphs, persistence schema, MAF capability mapping, and long-form design decisions belong in `docs/architecture.md`, not here.
126. Ordered implementation work belongs in `docs/roadmap.md` and the current execution details in `docs/Hive_Active_Work.md`.
127. Project status and performed verification belong in `docs/Hive_Current_Status.md`.
128. Detailed API usage belongs in source-level XML/API documentation or `docs/examples`.
129. Product-facing overview material belongs in README.md.
130. Do not turn this file into a duplicate manual, API reference, or architecture encyclopedia.

## Final operating rule

131. Make the smallest correct change that satisfies the active repository requirement, preserve the documented architecture, verify what can actually be verified, update only the source-of-truth documents whose state changed, and never claim work or evidence that did not occur.
