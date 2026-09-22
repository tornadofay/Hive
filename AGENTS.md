# Hive Repository Operating Constitution

This file governs AI coding-agent behavior in Hive. It is not the architecture manual, API reference, roadmap, or user guide.

Before changing the repository, an agent must follow this constitution and then consult the source-of-truth documents it points to.

## 1. Repository identity and actual baseline

1. Hive is a general-purpose C# / .NET 10 multi-agent platform. The V1 forcing function is image/document data entry into an existing business application; that use case does not redefine Hive's long-term scope.
2. The repository is an SDK-style .NET solution in `Hive.sln`. Platform libraries target `net10.0`; WinForms projects target `net10.0-windows`.
3. `Directory.Build.props` enables nullable reference types, implicit usings, latest C# language/analyzer settings, and `TreatWarningsAsErrors=true`. Preserve those contracts.
4. NuGet is used through project `PackageReference`. Current production packages are explicitly versioned; do not introduce floating dependency versions.
5. `Hive.Tests` is the authoritative automated test project and uses xUnit.
6. SQL Server is the current Hive persistence boundary; LocalDB is the supported local-development form of that boundary. Persistence implementation belongs in `Hive.Persistence`.
7. `System.Text.Json` is the repository's JSON serialization stack. Do not introduce a second JSON stack.
8. `Hive.Example.WinForms` is a permanent developer-facing/public-API example host, not an alternate test framework.
9. There is currently no CI/CD or release pipeline in the repository. Release automation is introduced when an actual release/operational need exists; do not fabricate CI, deployment, or release verification.
10. There is currently no separate deployment/package-release contract. Do not invent one outside an authorized slice or explicit request.
11. No generated-code pipeline or repository-local build script currently defines an additional source-of-truth. Do not invent one merely to support an agent workflow.

## 2. Source-of-truth hierarchy

12. `AGENTS.md` is authoritative for AI operating rules, scope control, verification honesty, and repository workflow.
13. `docs/architecture.md` is authoritative for intended architecture, ownership, dependency direction, and non-negotiable design rules.
14. `docs/Hive_Active_Work.md` is authoritative for the exact implementation slice currently allowed.
15. `docs/roadmap.md` is authoritative for ordered future implementation slices.
16. `docs/Hive_Current_Status.md` is authoritative for recorded project status and previously recorded verification.
17. Source/project files are authoritative for what is actually implemented right now.
18. Tests are evidence of behavior that was actually exercised; they are not permission to violate architecture.
19. README.md is explanatory/project-facing documentation and must not override the sources above.
20. `docs/examples` is public usage/reference material and must match the real APIs it demonstrates.
21. Architecture is normative; source code is implementation truth. If they disagree, report the mismatch rather than treating either one as permission to silently rewrite the other.
22. Never use previous chat history as repository authority. The repository must be self-describing enough for a new agent to establish the current state.

## 3. Required pre-change workflow

23. Read this file completely.
24. Read `docs/Hive_Current_Status.md` and `docs/Hive_Active_Work.md` before deciding what to implement.
25. Read the relevant roadmap slice in `docs/roadmap.md`.
26. Read the relevant architecture sections in `docs/architecture.md`. Read the full architecture document when the change crosses projects, public APIs, persistence, orchestration, lifecycle, security, or durable state.
27. Inspect the affected project files, project references, existing APIs, implementations, tests, examples, configuration, and package references before coding.
28. Determine the current checkpoint from repository state, not from task wording or memory.
29. Identify the smallest correct change and explicitly distinguish required work, necessary supporting work, optional improvements, and future work. Only the first two belong in the active change.
30. If there is no active implementation slice or the requested work conflicts with the active slice, do not silently select a new scope. Report the conflict and repository evidence.
31. Before editing, inspect the repository/Git state when the working environment provides it. Preserve unrelated local or developer changes.

## 4. Scope and change control

32. Implement only the active slice. Do not implement future roadmap items, later generations, speculative integrations, or unrelated cleanup.
33. A supporting change is allowed only when the active slice cannot be correctly implemented without it.
34. Do not refactor working unrelated code merely because a different design is cleaner, newer, shorter, or fashionable.
35. Do not add speculative abstractions, caches, frameworks, background services, compatibility layers, or extension points without a concrete current contract requiring them.
36. Prefer an existing Hive abstraction over a new abstraction when it owns the required responsibility.
37. If implementation reveals a broader improvement, record the finding and keep it out of the active change unless the current architecture/plan already authorizes it.
38. Do not silently change observable behavior, public API semantics, lifecycle, persistence semantics, or security boundaries.
39. Breaking changes, destructive migrations, security-boundary changes, dependency replacements, orchestration changes, or major architectural changes require an explicit architectural decision; do not invent that decision silently.
40. Human/project decisions already recorded in the repository remain authoritative until intentionally changed through the documented workflow.

## 5. Architecture and dependency protection

41. Preserve responsibility ownership and dependency direction defined by `docs/architecture.md`.
42. `Hive.Core` remains dependency-light and host/provider neutral. WinForms, SQL client, provider transport, and host-business dependencies do not belong there.
43. `Hive.Management` is the management/application facade. WinForms hosts must not bypass it for management/domain behavior.
44. Provider transport belongs behind provider adapters. Compatible hosted/local OpenAI-style targets share the common transport boundary rather than receiving separate transport implementations.
45. Use Microsoft Agent Framework wherever MAF already owns the required execution/orchestration mechanism. Hive must not become a second workflow engine.
46. Base `Agent` and `Hive` remain complete useful types. Cognitive generations are additive and must never become hidden prerequisites of base functionality.
47. Agent generation is selected explicitly at creation. Do not infer, promote, or demote Agent generations at runtime.
48. Hive state remains separate from host business data. Hive persistence is not an implicit gateway to the host application's business database.
49. Observation/discovery provides context only; it never grants authorization to mutate or invoke host controls.
50. `Hive.Host.WinForms.UI` owns Hive's WinForms presentation implementation and custom rendering. The current renderer is native WinForms plus custom `System.Drawing`; do not add a third-party renderer without an explicit architecture decision.
51. Consuming WinForms projects use Hive-owned UI contracts and controls. Do not leak renderer implementation details across that boundary.
52. Do not create Hive-prefixed wrappers for ordinary WinForms controls unless Hive needs a real consumer-facing contract or behavior beyond the framework control.
53. Direct project references do not by themselves prove that architecture is being bypassed. Inspect actual type usage before changing references. When modifying the Host.WinForms dependency graph, reconcile it with the documented Management boundary rather than silently normalizing it.

## 6. Dependency and technology policy

54. Inspect existing project references and package versions before adding or replacing a dependency.
55. Prefer the .NET/framework capability, Microsoft Agent Framework, or an existing Hive boundary when it already supplies the required behavior.
56. A new production dependency requires a concrete current need, a clear ownership boundary, a compatible supported version, and acceptable maintenance/license implications.
57. Do not upgrade a framework or package merely because a newer version exists. Upgrade only for the active requirement, a justified security/compatibility need, or an explicitly approved modernization decision.
58. Do not replace working technology for stylistic preference. Compare contracts, support, compatibility, migration cost, and operational impact first.
59. When an API or package behavior is version-sensitive, verify it against the repository's actual target/package references and authoritative documentation rather than memory.
60. Experimental or deprecated APIs require explicit justification and must not be introduced accidentally through copied examples.

## 7. Production engineering rules

61. Preserve nullable contracts. Do not weaken nullability or use broad null-forgiving operators to silence diagnostics.
62. Keep public APIs deliberately small. Prefer immutable value objects/snapshots across architectural boundaries and avoid unnecessary exposure of mutable state.
63. Preserve async behavior and cancellation semantics. Do not introduce sync-over-async, blocking waits, arbitrary sleeps, or unnecessary `Task.Run`.
64. External I/O must be cancellation-aware and bounded by explicit timeout/budget semantics where the API permits them.
65. Retries must be finite, classified, cancellation-aware, and used only for operations whose duplicate/side-effect behavior is safe.
66. Durable side effects and replayable operations require explicit idempotency/duplicate handling. Do not rely on exactly-once assumptions across process, network, queue, outbox, or provider boundaries.
67. Dispose owned resources deterministically, including database objects, streams, timers, WinForms controls, images, GDI objects, and asynchronous disposables.
68. Never use empty catch blocks or silently swallow failures.
69. Boundary failures must preserve useful context and stable classification. Do not collapse validation, authorization, conflict, timeout, cancellation, capacity, transport, provider, and internal failures into one undifferentiated result.
70. Authorization is enforced at the authoritative code boundary. Prompts, UI visibility, capability metadata, scope matching, or caller intent are not authorization.
71. Treat user-controlled and external data as untrusted. Validate size, shape, encoding, identifiers, paths/URIs, parser limits, and resource limits before expensive or privileged operations.
72. Configuration that affects correctness, security, persistence, limits, provider selection, or execution must be validated where it enters the system. Do not silently fall back to a materially different or unsafe configuration.
73. Secrets, credentials, access tokens, sensitive provider payloads, and unnecessary business/personal data must not appear in logs, Example output, telemetry, test output, or durable diagnostics.
74. Do not invent a logging framework merely to satisfy a documentation rule. Use the repository's existing diagnostic boundary when one exists; otherwise keep diagnostics proportional to the active contract.
75. Configuration must actually drive the behavior it claims to configure. Accepted-but-ignored configuration is an incomplete implementation.

## 8. Persistence, schema, and durable-state rules

76. SQL access belongs inside `Hive.Persistence` or an explicitly authorized persistence boundary; do not put database code in `Hive.Core` or forms.
77. Use parameterized SQL, bounded queries, real indexes for repeated lookups, and database constraints for invariants that must hold under concurrency.
78. Use explicit transactions for atomic state changes. Application-side validation does not replace required database constraints.
79. Migrations must be explicit, ordered, repeatable, and compatible with the repository's DbUp/schema-version strategy.
80. Event payload schema versions and database schema versions are separate contracts. Preserve supported historical event readability through upcasting rather than rewriting history.
81. Durable workflows must have defined recovery/checkpoint semantics. Restart, retry, and late completion must not duplicate terminal side effects.
82. Do not introduce a new database technology or persistence engine unless the active architecture/roadmap explicitly authorizes it.
83. Do not silently change durable schemas or stored-data semantics. Treat migrations and persisted contracts as compatibility surfaces.

## 9. Concurrency and WinForms rules

84. Shared mutable state requires explicit ownership and synchronization. Do not introduce global mutable state without a defined concurrency model.
85. Late async completion must not mutate disposed UI, terminal execution state, superseded configuration, or resources no longer owned by the operation.
86. WinForms controls are accessed only on their owning UI thread. Keep UI handlers responsive and move blocking external work off the UI thread.
87. Release event subscriptions and owned resources deterministically. Cancellation/disposal must prevent stale callbacks from updating dead UI.
88. Avoid unnecessary control-tree traversal, layout/painting churn, handle recreation, allocations, and synchronization overhead on repeated UI paths.
89. Optimize based on a demonstrated contract, measured cost, or clear hot path; do not add speculative micro-optimizations that reduce clarity.

## 10. Example Host and shared UI rules

90. Example implementations in designated assemblies must satisfy the actual discovery contract: `IHiveExample`, required metadata, `CreateView(IServiceProvider)`, and a parameterless constructor unless the discovery mechanism is deliberately changed.
91. Example discovery is reflection-based and may silently omit invalid/unconstructible types. After adding an example, verify that it appears in navigation.
92. Example ordering is deterministic: `Order`, then `Category`, then `Subcategory`, then `Title`, with case-insensitive text comparison.
93. Examples access shared services through the established typed `IServiceProvider` extensions such as `GetThemeManager()` and `GetExampleOutput()`. Do not call `services.GetService(...)` directly from example implementations.
94. Ordinary operation/test examples should reuse `HiveExampleTestSurface` and the shared Example output service/view. Specialized examples such as CRUD, dialogs, and theme demonstrations may keep their own feature-specific presentation.
95. CRUD examples/pages use `HiveCrudPage<TItem>` and shared editor/layout primitives when those contracts fit. Domain schema, validation, authorization, persistence, and specialized editors remain consumer-owned.
96. Examples must demonstrate public platform boundaries, not private shortcuts or host-only assumptions unavailable to a real consumer.
97. Shared Example Host infrastructure must remain feature-neutral. Do not add feature-specific logic to discovery, shell navigation, or shared test/output controls merely to simplify one example.
98. UI polish changes must preserve navigation state, theme state, responsive behavior, focus/selection semantics, disposal, and resource ownership; do not reopen completed UI foundation scope without a concrete defect.

## 11. Testing and verification policy

99. Every implementation slice must satisfy the verification gate defined by its active roadmap/plan and architectural boundary.
100. Tests should cover the contract-relevant normal, invalid, boundary, cancellation, concurrency, recovery, persistence, compatibility, and security cases.
101. `Hive.Tests` is the authoritative automated suite. Example test tooling is developer convenience only and must not embed xUnit runner internals.
102. Automated provider/network tests use fakes or local deterministic infrastructure. Never use real vendor credentials or uncontrolled vendor accounts in automated tests.
103. Persistence integration tests use the repository's explicit SQL Server/LocalDB strategy and must be deterministic and isolated.
104. UI behavior changed by a slice requires actual developer/manual verification where the roadmap calls for it. Static inspection is not manual UI verification.
105. Publicly meaningful capabilities should include a copyable example and expected result where that improves consumer understanding.
106. Verification status must be stated precisely as one or more of: inspected/reasoned, compiled, automated-tested, integration-tested, manually verified, or not verified.
107. By default, AI agents must not run builds, tests, application launches, database migrations, or external integration calls unless the user explicitly authorizes execution or the repository workflow for the requested task explicitly requires agent-run verification. Never claim execution that did not happen.
108. Successful compilation is not evidence of runtime correctness, concurrency safety, persistence correctness, security, UI correctness, or production readiness.

## 12. Documentation synchronization

109. Update `docs/architecture.md` before structural code changes when the current architecture does not already authorize the exact change.
110. Update `docs/Hive_Active_Work.md` when active scope, dependencies, constraints, or verification requirements change.
111. Update `docs/Hive_Current_Status.md` only when actual status, completion, acceptance, or performed verification changes.
112. Update `docs/roadmap.md` when implementation order or slice definitions change. Do not use it as a development diary.
113. Update README.md when project direction, public structure, or primary usage changes materially. Do not copy detailed architecture into it.
114. Update `docs/examples` and public API documentation when a completed public contract changes their usage.
115. Do not document planned behavior as implemented. Documentation must match the actual current implementation.
116. When a source-of-truth conflict is discovered, fix the authoritative document/code pair intentionally rather than applying wording-only changes that leave the contradiction in place.

## 13. Final change review and completion

117. Before completion, inspect the final diff and affected files for accidental edits, unrelated cleanup, dead code, stale comments, duplicate logic, missed disposal, missed unsubscription, and generated-file mistakes.
118. Review the change against the requested behavior, active slice, architecture, dependency direction, public API compatibility, persistence, security, concurrency, cancellation, observability, performance, and recovery as applicable.
119. Confirm that new behavior is reachable through its intended public boundary and that configuration actually controls it.
120. Confirm that required tests, examples, and documentation for the changed contract exist and that their verification status is known.
121. Separate current blockers from future improvements. Do not hide unresolved limitations merely because the active slice is otherwise functional.
122. A slice is complete only when its implementation, documentation, and required verification gates are actually satisfied.

## 14. Git and repository hygiene

123. Normal continuation work is performed on the repository's current primary branch `main`. Do not create feature branches or pull requests unless explicitly instructed.
124. Preserve unrelated local/developer changes. Never reset, clean, overwrite, or force-resolve work you did not create.
125. Do not rewrite published history or force-push unless explicitly instructed.
126. Keep each commit focused on one completed logical change. Do not mix future-slice work, unrelated refactors, formatting churn, or dependency upgrades into it.
127. Do not commit secrets, local environment files, build artifacts, test results, machine-specific output, or ignored files unless the repository explicitly requires them.
128. Generated files must be changed through their authoritative generation path when one exists.
129. If the remote `main` changes concurrently, refresh/reconcile before updating it. Never overwrite newer remote work merely to preserve an agent's local result.

## 15. AI failure prevention and escalation

130. Do not guess when repository evidence is available. Inspect the relevant file, project reference, test, or architecture section.
131. Do not invent APIs, package behavior, commands, framework capabilities, or configuration semantics. Verify them against actual references and authoritative documentation.
132. Do not silently replace an established project decision because an alternative seems preferable.
133. Do not silently broaden scope when implementation reveals a better design. Record the finding and keep it outside the active slice unless authorized.
134. Do not optimize, abstract, upgrade, or rewrite merely because it appears technically nicer.
135. Do not fabricate build/test/manual/integration evidence. If work was not executed, state that it was not executed.
136. For an architectural conflict, ambiguous public contract, breaking change, destructive migration, security boundary, dependency replacement, or external contract change, report the conflict, repository evidence, viable options, and impact before choosing a new direction.
137. If a safe, local implementation can proceed without resolving the larger issue, do only that bounded work and leave the unresolved decision explicit.
138. Never rely on hidden context or previous conversations for essential project state.

## 16. What belongs elsewhere

139. Detailed architecture, domain models, lifecycle semantics, dependency graphs, MAF mapping, persistence schema, and long-form design decisions belong in `docs/architecture.md`.
140. Ordered implementation work belongs in `docs/roadmap.md`; current execution scope belongs in `docs/Hive_Active_Work.md`.
141. Project status and performed verification belong in `docs/Hive_Current_Status.md`.
142. Detailed public API usage belongs in source documentation and `docs/examples`.
143. Product-facing overview belongs in README.md.
144. Do not turn `AGENTS.md` into an architecture encyclopedia, API manual, coding-style catalog, or user guide.

## Final operating rule

145. Make the smallest correct change that satisfies the active repository requirement, preserve the documented architecture, verify only what was actually verified, synchronize only the source-of-truth state that changed, and keep unresolved architectural decisions visible rather than silently deciding them.
