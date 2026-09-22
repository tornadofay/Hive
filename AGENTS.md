# Hive Repository Operating Constitution

This file governs AI coding-agent behavior in the Hive repository.

It is a repository operating constitution, not an architecture encyclopedia, API manual, roadmap, coding-style guide, or product specification. Detailed knowledge remains in the source-of-truth documents named below.

Before changing any repository file, an AI agent must follow this constitution.

## 1. Repository baseline

Hive is a general-purpose C# / .NET 10 platform for building, running, coordinating, observing, governing, and evolving multi-agent systems. The current V1 forcing function is document/image data entry into an existing business application; that use case does not redefine Hive's long-term scope.

Current repository baseline:

- Solution: Hive.sln.
- Platform libraries target net10.0.
- WinForms projects target net10.0-windows.
- Repository-wide nullable reference types, implicit usings, latest language/analyzer settings, and warnings-as-errors are enabled by Directory.Build.props.
- NuGet dependencies are declared with project PackageReference; versions are explicit.
- Hive.Tests is the authoritative automated test project and uses xUnit.
- SQL Server is the current Hive persistence boundary; LocalDB is the supported local-development form of that same boundary.
- System.Text.Json is the repository JSON stack.
- Hive.Example.WinForms is a permanent developer-facing/public-API example host, not an alternate test framework.
- There is currently no repository CI/CD or release pipeline, no separate deployment/package contract, and no repository-local build/release script that overrides the documented workflow.
- There is no separate generated-code pipeline currently defining another source of truth.

Do not turn these baseline facts into hard-coded version claims. Project files and authoritative documentation remain the evidence for current dependency versions and implementation details.

## 2. Source-of-truth hierarchy

Authority is responsibility-specific:

1. AGENTS.md — how an AI agent must operate.
2. docs/architecture.md — intended architecture, ownership, dependency direction, security boundaries, lifecycle rules, and durable-state design.
3. docs/Hive_Active_Work.md — the only currently authorized implementation slice and its verification gate.
4. docs/roadmap.md — ordered future implementation slices.
5. Source/project files — what is actually implemented.
6. Tests — evidence of behavior actually exercised by tests.
7. docs/Hive_Current_Status.md — recorded status and historical verification; it does not authorize work or override source evidence.
8. docs/examples/ — public usage/reference material.
9. README.md — project-facing overview; it must not override the sources above.

When sources disagree:

- Architecture governs intended structural direction.
- Source/project files govern implementation reality.
- Active Work governs current scope.
- Roadmap governs order, not permission to skip the active slice.
- Status records what has happened; it does not make an unverified claim true.
- If architecture and implementation disagree, do not silently choose a new design. Determine whether the task is to reconcile the mismatch; if so, make the smallest explicit correction and synchronize the authoritative document.
- Never use previous chat history as repository authority.

## 3. Required reading and pre-change workflow

Before implementation:

1. Read this file completely.
2. Read docs/Hive_Current_Status.md and docs/Hive_Active_Work.md.
3. Read the relevant slice in docs/roadmap.md.
4. Read the relevant architecture sections in docs/architecture.md. Read the full architecture document when the change crosses projects, public APIs, persistence, orchestration, lifecycle, security, or durable state.
5. Inspect the affected project files, project references, source code, tests, examples, configuration, and package references.
6. Inspect the repository state available to the agent and preserve unrelated developer work.
7. Establish the current checkpoint from repository evidence.
8. If docs/Hive_Active_Work.md names an active slice, continue that slice. Do not select a later slice merely because it is the next item in the roadmap.
9. If the active slice is actually closed, determine the next authorized slice from the roadmap and update Active Work before implementation.
10. Classify intended work as:
   - required change;
   - necessary supporting change;
   - optional improvement;
   - future work.
11. Only required and necessary supporting changes may enter the active implementation.
12. Before editing, identify the smallest correct change that satisfies the active requirement.

### Slice execution and handoff

A normal implementation slice follows this lifecycle:

1. **Implement** the complete behavior required by the active slice, not a temporary or knowingly incomplete substitute.
2. **Test** by adding/updating the contract-relevant automated tests required by the slice.
3. **Example** by adding/updating the matching public/developer-facing example for every new meaningful capability that is externally usable. The Example must exercise the capability through Hive's public APIs.
4. **Review** the final change against the active slice, architecture, persistence, concurrency, security, compatibility, documentation, and unintended side effects.
5. **Handoff** the repository in a runnable verification state for developer execution when agent-run verification is not authorized.
6. **Do not close the slice merely because implementation is finished.** Required verification remains open until the authorized build/tests/manual verification are actually performed.
7. When the developer supplies verification results, reconcile those results with the repository and then update the owning status/active-work documents. Record only results actually reported or independently executed.
8. Close the active slice and advance to the next slice only after the implementation, required tests/examples, documentation, and verification gate are satisfied.

### Mandatory Example and test handoff

- Every new meaningful capability must have corresponding verification in **Hive.Tests**.
- Every new meaningful capability that is externally usable must also have a corresponding **Hive.Example.WinForms** scenario in the same implementation run.
- The Example must exercise the capability through public APIs and be independently understandable and reproducible.
- A new-capability implementation run is incomplete if the required Example scenario does not exist. Do not move to verification handoff or declare the checkpoint complete until it exists.
- The final handoff MUST contain an exact `Example to run:` line naming the Example UI path/title and supported target(s), whenever a capability requires an Example.
- The final handoff MUST contain an exact `Tests to run:` line naming the focused test class/file and stating whether broader test execution is required by the active slice.
- When verification is pending, `docs/Hive_Active_Work.md` MUST preserve the exact Example UI path/title and focused test class/file needed for the developer to execute the checkpoint without rediscovering them.

Whenever implementation changes the active slice's scope, constraints, dependencies, required tests/examples, verification state, handoff state, or completion state, update docs/Hive_Active_Work.md in the same logical change. Do not leave Active Work describing an earlier repository state.

Do not begin implementation merely because the task wording suggests a design. Verify the current repository first.
## 4. Scope and change control

The default rule is:

Make the smallest correct change that satisfies the active requirement.

AI agents must not:

- implement future roadmap slices;
- reopen completed phases without a concrete defect or active requirement;
- refactor unrelated working code;
- perform drive-by cleanup;
- introduce speculative abstractions or extension points;
- upgrade dependencies without a concrete reason;
- replace working technology for stylistic preference;
- silently change observable behavior;
- silently change persistence or compatibility semantics;
- silently widen the task because a better design was discovered.

When a broader improvement is discovered:

- record the finding;
- explain why it matters;
- keep it outside the active change unless already authorized.

A supporting change is permitted only when the active requirement cannot be implemented correctly without it.

Repository-governance work is a bounded exception to feature-slice scope: an explicit task to modify AGENTS.md, reconcile a source-of-truth document, or correct repository governance may change those governance/documentation files even when a different feature slice is active. It must not be used as permission to modify unrelated production code.


## 5. Architecture and dependency protection

Preserve the responsibility boundaries in docs/architecture.md.

### Core and platform layers

- Hive.Core stays dependency-light and host/provider neutral.
- SQL client, WinForms, provider transport, vendor-specific SDKs, and host-business dependencies do not belong in Hive.Core.
- Agent, persistence, tools, and provider projects may build on Core according to the documented dependency graph.
- Hive.Management is the management/application facade for domain and management operations.
- Hive.Host.WinForms must not directly reference Hive.Core or Hive.Agents. Management/domain behavior flows through Hive.Management; presentation infrastructure flows through Hive.Host.WinForms.UI.
- Hive.Host.WinForms.UI owns Hive's WinForms presentation implementation.
- No core/platform project may depend on Hive.Example.WinForms.

### MAF and orchestration

Use Microsoft Agent Framework wherever MAF already owns the required execution/orchestration mechanism.

Do not build a second workflow/orchestration engine merely to reproduce framework behavior. If Hive needs a higher-level contract that MAF does not provide, keep Hive's layer explicit and replaceable where the architecture requires future substitution.

### Agent generations

- Base Agent and base Hive remain complete useful types.
- Cognitive generations are additive, not prerequisites.
- Generation is selected explicitly at creation.
- Do not infer, promote, or demote Agent generations at runtime.

### UI

- Current Hive UI uses native WinForms behavior plus custom System.Drawing rendering inside Hive.Host.WinForms.UI.
- Do not introduce a third-party renderer without an explicit architecture decision.
- Do not leak renderer implementation details into consuming projects.
- Do not wrap ordinary WinForms controls merely to rename them. Create a Hive-owned control only when Hive needs a real consumer-facing behavior, styling contract, or capability.
- UI consumers must preserve theme state, selection/focus state, responsive behavior, resource ownership, and thread affinity.

### Persistence and host boundaries

- All SQL/database access stays inside Hive.Persistence or another explicitly authorized persistence boundary.
- Hive persistence is separate from the host application's business database.
- Host business/domain state remains host-owned.
- Observation or control discovery provides context; it never grants authorization to mutate or invoke host controls.

## 6. Existing abstraction before new abstraction

Before creating a class, service, interface, helper, framework integration, cache, wrapper, or provider boundary:

1. Find the existing responsibility owner.
2. Determine whether the current abstraction already satisfies the requirement.
3. Reuse it when the contract fits.
4. Introduce a new abstraction only when the active requirement creates a genuine boundary or missing contract.
5. Document the architectural reason when a new boundary changes dependency direction or public API surface.

Prefer:

existing architecture > new abstraction

Do not create abstractions solely to make one small implementation shorter.

## 7. Dependencies and external technology

Before adding or replacing a dependency, inspect the existing project references and package versions.

A new production dependency requires:

- a concrete current requirement;
- a clear ownership boundary;
- compatibility with the target framework and repository architecture;
- an appropriate supported version;
- reasonable maintenance and license implications.

Rules:

- Prefer the .NET/framework capability, MAF, or an existing Hive boundary when it already provides the required behavior.
- Do not introduce a library to avoid a small amount of straightforward code that the repository can already implement safely.
- Do not upgrade frameworks or packages merely because newer versions exist.
- Experimental or deprecated APIs require explicit justification.
- Do not add vendor-specific transport implementations when the documented common provider boundary already covers the target.
- Verify version-sensitive API behavior against actual project references and authoritative documentation when necessary.

## 8. Production engineering rules

Apply production engineering proportional to the active boundary.

### Contracts and APIs

- Preserve nullable contracts.
- Keep public APIs intentionally small.
- Prefer immutable value objects and snapshots across architectural boundaries.
- Do not expose mutable implementation state without a concrete ownership contract.
- Preserve compatibility unless the active slice intentionally changes the contract.

### Errors and diagnostics

- Never silently swallow failures.
- Never use empty catch blocks.
- Preserve useful boundary context and stable error categories.
- Do not collapse materially different failures into a generic success/failure string.
- Do not invent a logging framework merely to satisfy this constitution. Use the repository's existing diagnostic boundary; keep new diagnostics proportional to the actual contract.
- Diagnostics must not expose secrets or unnecessary sensitive data.

### Async, cancellation, concurrency

- Preserve asynchronous behavior and cancellation semantics.
- Do not introduce sync-over-async, arbitrary sleeps, or unnecessary Task.Run.
- External I/O must be cancellation-aware and bounded by explicit timeout/budget semantics where supported.
- Shared mutable state requires explicit ownership and synchronization.
- Late completions must not mutate terminal state, superseded configuration, disposed UI, or resources no longer owned by the operation.
- WinForms controls may only be accessed on their owning UI thread.

### Reliability

- Retries must be finite, classified, cancellation-aware, and safe for the operation's side-effect semantics.
- Side-effecting or durable operations must define duplicate/idempotency behavior.
- Do not assume exactly-once execution across process, network, queue, provider, or outbox boundaries.
- Durable workflows must define recovery/checkpoint behavior where the architecture requires it.
- Terminal state must not be overwritten by late work.

### Resources and performance

- Dispose owned streams, database objects, timers, WinForms controls, images, GDI objects, and asynchronous disposables deterministically.
- Release event subscriptions deterministically.
- Avoid unnecessary UI layout, handle recreation, control-tree traversal, painting, allocation, or synchronization churn.
- Optimize measured or contractually important costs, not hypothetical micro-benchmarks.
- Do not hide expensive I/O in property getters, formatting, or rendering paths.

### Security and input

- Authorization is enforced at the authoritative code boundary. Prompts, UI visibility, capability metadata, scope matching, and caller intent are not authorization.
- Treat user-controlled and external data as untrusted.
- Validate shape, size, encoding, identifiers, paths/URIs, parser limits, and resource limits before expensive or privileged operations.
- Secrets, credentials, tokens, and sensitive provider/business data must not appear in source control, logs, telemetry, examples, or test output.
- Configuration that affects correctness, security, persistence, provider selection, limits, or execution must be validated where it enters the system.
- Accepted-but-ignored configuration is an incomplete implementation.

## 9. Persistence and schema rules

- SQL uses parameterization and bounded queries.
- Repeated lookup paths require appropriate indexes.
- Database constraints must protect invariants that must hold under concurrency.
- Atomic state changes use explicit transaction boundaries.
- Application-side validation does not replace required database constraints.
- Migrations are explicit, ordered, repeatable, and compatible with the repository's DbUp/schema-version strategy.
- Database schema versioning and event-payload schema versioning are separate contracts.
- Supported historical event payloads remain readable through upcasting rather than rewriting history.
- Never silently change durable schemas or persisted-data semantics.
- A destructive migration, incompatible schema change, or durable contract break requires an explicit architectural decision before implementation.

## 10. Example Host and shared UI rules

Hive.Example.WinForms is the permanent developer-facing/public-API example surface.

When working in the Example Host:

- Follow the actual IHiveExample discovery contract and constructor requirements.
- Keep discovery deterministic and feature-neutral.
- Preserve Category → Subcategory → Example navigation behavior.
- Use the established typed IServiceProvider extensions for shared services rather than direct GetService(...) calls from examples.
- Ordinary operation examples should reuse the shared Example test/output surface where it fits.
- Specialized CRUD, dialogs, and theme examples may retain feature-specific presentation.
- Generic CRUD UI orchestration remains domain-neutral: consuming features own schema, validation, authorization, persistence, and specialized editors.
- Examples demonstrate public platform contracts, not private shortcuts.
- Every new meaningful externally usable capability must have its corresponding Example scenario in this host during the same implementation run.
- Do not embed xUnit runner internals in the Example Host.
- Static code inspection is not evidence that a new example was manually verified as visible/usable.

## 11. Testing and verification

Hive.Tests is the authoritative automated suite.

Test requirements are determined by the active slice and affected boundary. As applicable, cover:

- normal, invalid, and boundary contracts;
- duplicate/malformed state;
- ownership and authorization;
- cancellation and timeouts;
- concurrency and stale-state races;
- persistence transactions and indexes;
- recovery and replay;
- provider/network failures using fakes or controlled local infrastructure;
- compatibility and upcasting;
- security-sensitive behavior;
- public API Example scenarios for every new meaningful capability that is externally usable; these are mandatory matching verification alongside Hive.Tests.

Persistence integration tests use the repository's SQL Server/LocalDB strategy. Provider tests must not use real vendor credentials or uncontrolled vendor accounts.

UI changes require actual developer/manual verification when the active slice calls for it. No UI-automation framework is required by the current architecture.

### Verification honesty

Use these exact categories when reporting verification:

- Inspected — source/project/document inspection only.
- Reasoned — behavior derived from inspection without execution.
- Compiled — an actual build completed successfully.
- Automated-tested — automated tests actually ran and produced the reported result.
- Integration-tested — an integration boundary actually ran.
- Manually verified — a developer actually exercised the UI/behavior.
- Not verified — not executed or not otherwise established.

Never imply one category proves another. A successful compile does not prove runtime, persistence, security, concurrency, or UI correctness.

### AI execution default

AI agents must not run builds, tests, application launches, database migrations, performance measurements, or external integration calls unless:

- the user explicitly authorizes execution; or
- the repository workflow for the requested task explicitly requires the agent to perform that verification.

When execution is not authorized, inspect and reason without pretending execution occurred.

Never fabricate test counts, build results, manual verification, provider responses, migration results, or performance measurements.

## 12. Documentation synchronization

Update documentation only when the implementation changes the state that document owns.

- docs/architecture.md — structural ownership, dependency direction, lifecycle, public architectural contracts, persistence design, or other intended architecture.
- docs/Hive_Active_Work.md — current implementation scope, constraints, and verification gate.
- docs/roadmap.md — implementation order and slice definitions.
- docs/Hive_Current_Status.md — actual status, completion, and performed verification only.
- docs/examples/ — public usage/reference examples.
- README.md — material project-facing direction or public usage changes.

An explicit repository-governance or documentation-reconciliation task is allowed to update the owning source-of-truth document even when the feature roadmap is elsewhere. Such work must remain limited to the inconsistency or governance requirement being corrected.

Rules:

- Do not document planned behavior as implemented.
- Do not use status documents as planning notes.
- Do not copy architecture encyclopedias into AGENTS.md.
- If code and documentation conflict, reconcile the responsible source-of-truth pair intentionally.
- When a change alters a public contract, update public examples/API documentation in the same logical change when applicable.

## 13. Architectural escalation

An AI agent may implement locally bounded work without escalation when the architecture and contract are clear.

Do not silently choose a new architectural direction for:

- conflicting architecture decisions;
- ambiguous public API semantics;
- breaking public API changes;
- security-boundary changes;
- dependency replacements;
- new persistence engines;
- destructive or incompatible migrations;
- major orchestration/workflow changes;
- external protocol/host/provider contract changes.

For an escalation, identify:

1. the conflict or missing decision;
2. repository evidence;
3. viable options;
4. impact and compatibility consequences;
5. the smallest safe action available without making the unresolved decision.

If safe bounded work can proceed, do only that work. Otherwise stop before making the structural decision.

## 14. Git and repository hygiene

- Normal continuation work is performed directly on main unless explicitly instructed otherwise.
- Do not create feature branches or pull requests unless explicitly requested.
- Preserve unrelated developer changes.
- Never reset, clean, overwrite, or force-resolve work you did not create.
- Never rewrite published history or force-push unless explicitly instructed.
- Keep commits focused on one completed logical change.
- Do not mix future-slice work, unrelated cleanup, dependency upgrades, or formatting churn into a focused commit.
- Do not commit secrets, local environment files, build artifacts, test results, machine-specific output, or ignored files unless the repository explicitly requires them.
- Do not hand-edit generated output when an authoritative generation path exists.
- If remote main changed concurrently, reconcile before updating it; never overwrite newer remote work merely to preserve an agent's result.

## 15. Final review and completion

Before declaring a change complete:

1. Review the final diff and affected files.
2. Check for accidental edits, dead code, stale comments, duplicated logic, missed disposal, missed unsubscription, and generated-file mistakes.
3. Re-check the requested behavior and active-slice scope.
4. Re-check architecture and dependency direction.
5. Re-check public API compatibility, persistence, security, concurrency, cancellation, diagnostics, configuration, and recovery as applicable.
6. Confirm configuration actually affects behavior where relevant.
7. Confirm the required tests/examples/documentation for the changed contract exist.
8. Separate current blockers from future improvements.
9. Report exactly what changed.
10. Report exactly what verification was performed.
11. Explicitly report what remains unverified.

A slice is complete only when its implementation, required documentation, and required verification gate are actually satisfied.

## 16. AI failure prevention

These are hard prohibitions:

- Do not guess when repository evidence is available.
- Do not invent APIs, package behavior, commands, framework features, or configuration semantics.
- Do not assume architecture from folder names alone.
- Do not treat a project reference as proof of behavioral use; inspect actual usage when dependency direction matters.
- Do not overwrite established project decisions because an alternative looks cleaner.
- Do not broaden scope because a larger refactor appears desirable.
- Do not optimize without a demonstrated requirement or architectural reason.
- Do not fabricate build, test, manual, integration, performance, or release evidence.
- Do not hide uncertainty or known limitations.
- Do not silently change behavior, compatibility, authorization, persistence, or lifecycle semantics.
- Do not rely on previous conversation context for essential project state.

## 17. What belongs elsewhere

Keep detailed knowledge out of this file:

- architecture, domain models, dependency graphs, lifecycle semantics, MAF mapping, persistence schema, and long-form design decisions → docs/architecture.md;
- ordered implementation work → docs/roadmap.md;
- current execution scope → docs/Hive_Active_Work.md;
- project status and performed verification → docs/Hive_Current_Status.md;
- detailed public API usage → source/API documentation and docs/examples/;
- project overview → README.md;
- test implementation details → Hive.Tests and the relevant test/documentation boundary.

If a rule needs many paragraphs to explain the domain itself, it probably belongs in another document.

## Final operating rule

Make the smallest correct change that satisfies the active repository requirement, preserve the documented architecture, use repository evidence instead of assumptions, verify only what was actually verified, synchronize only the source-of-truth state that changed, and keep unresolved architectural decisions visible rather than silently deciding them.
