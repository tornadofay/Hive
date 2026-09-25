# Hive AI Agent Operating Rules

This file is the repository operating constitution. Keep it compact. Detailed architecture, APIs, UI guidance, and review checklists belong in their owning documents.

## 1. Authority

These sources have different authoritative roles:

1. `AGENTS.md` — workflow and non-negotiable repository rules.
2. `docs/architecture.md` and relevant `docs/architecture/*.md` — intended architecture and ownership.
3. `docs/Hive_Active_Work.md` — current authorization, implementation slice, and verification gate.
4. `docs/roadmap.md` — ordered future work, not authorization.
5. Source/project files — implementation reality.
6. Tests — behavior actually exercised.
7. `docs/Hive_Current_Status.md` — current status only.
8. `docs/verification/` — historical verification evidence.
9. `docs/ui/` and `docs/examples/` — usage guidance.
10. `README.md` — project overview.

Do not treat this as a simple precedence chain across different concerns: architecture defines intended structure, Active Work defines what is authorized now, source defines implementation reality, tests define what was exercised, roadmap defines future order, and status never turns an unverified claim into a fact. When sources conflict, apply the role that governs the decision and resolve genuine inconsistencies through the repository workflow. Previous chat history is not repository authority.

## 2. Before changing code

Read, in order:

1. this file;
2. `docs/Hive_Current_Status.md`;
3. `docs/Hive_Active_Work.md`;
4. the relevant roadmap section;
5. the relevant architecture sections;
6. the relevant UI guidance for UI/Example work.

For cross-project, public-API, persistence, orchestration, lifecycle, security, or durable-state work, read the full applicable architecture documents.

Then inspect affected projects, source, tests, examples, configuration, references, and responsibility owners. Determine the repository checkpoint from evidence.

If Active Work is `VERIFICATION PENDING` before actual developer results arrive, stop implementation-affecting work at that gate. After a real failure is recorded as `VERIFICATION FAILED / REMEDIATION REQUIRED`, same-slice Revision/remediation may proceed only within the recorded failure boundary; it must return Active Work to `VERIFICATION PENDING` for developer re-verification.

## 3. Scope and authorization

Active Work is the maximum implementation boundary.

- Continue, Revision, Again, and Polish inherit the current task/scope. Maintenance does the same when Active Work is open; with no active slice, an explicit bounded corrective Maintenance request may create a temporary slice under the rule below. **Workflow Review** is read-only and does not modify repository state or create authorization; it is not the V1/future business Review lifecycle or capability. **Revision is corrective:** it re-audits the preceding work and fixes concrete in-scope problems to production depth. When Revision follows Workflow Review, the reported findings may be corrected within the applicable authorized boundary; the Review itself does not authorize unrelated work or roadmap advancement. None advances the roadmap.
- A new roadmap slice requires explicit user authorization such as `Hive: Start Phase X.Y`.
- Do not silently widen scope, replace the current slice, or implement future work.
- If Active Work is closed/absent, an explicitly requested bounded non-roadmap corrective task may create a temporary Active Work slice before implementation, but only when repository evidence confirms it restores, preserves, or corrects existing behavior without adding capability or materially expanding a public contract. **Revision is such a corrective request when it follows a preceding implementation task:** it may establish the temporary slice needed to re-audit that work and correct concrete findings discovered during Revision. If a finding requires a new capability, material public-contract expansion, or roadmap work, stop that portion and require separate authorization.
- **Workflow-documentation work is separate from implementation Active Work:** Revision of `AGENTS.md`, `.agents/skills/`, or other workflow-owned documents remains bounded to those governance documents and does not create an implementation Active Work slice. It may update affected workflow documentation directly while preserving implementation authorization/state.
- Mixed corrective + feature work does not qualify for auto-opening unless the user explicitly separates the scopes.
- If a corrective task discovers work requiring a new capability, material public-contract expansion, or another roadmap slice, stop that portion and require separate authorization.
- A governance/workflow-documentation task may update its affected documentation even when implementation Active Work is open, but must not alter unrelated implementation or manufacture authorization.

## 4. Verification

Verification is a hard gate between implementation and evidence.

When developer results arrive:

1. all required checks pass → close through the normal evidence-backed workflow;
2. failures, compiler errors, or unmet required behavior within scope → record `VERIFICATION FAILED / REMEDIATION REQUIRED` in Active Work **before** changing implementation;
3. perform same-slice remediation to production depth;
4. return Active Work to `VERIFICATION PENDING` with the exact rerun targets;
5. require developer re-verification.

Out-of-scope or new-capability failures require separate authorization.

Never claim a build, test, integration, manual check, or provider result that did not actually happen.

Use these terms precisely: Inspected, Reasoned, Compiled, Automated-tested, Integration-tested, Manually verified, Not verified.

By default, do not run builds, tests, launches, migrations, performance measurements, or external integrations. A verification requirement defines what the developer must verify; it does not by itself authorize the agent to execute it.

## 5. Engineering standard

Keep implementation scope bounded, but make the engineering work complete and production-grade within that scope. Do not optimize for the fewest lines or files.

Within the affected boundary, correct root causes and relevant supporting problems involving correctness, contracts/nullability, validation/errors, cancellation/async behavior, concurrency, lifecycle/disposal, determinism/recovery, observability, performance/I/O, authorization/security, persistence, compatibility, tests, examples, or documentation as applicable.

Do not perform unrelated refactoring, speculative abstraction, dependency upgrades without requirement, or symptom-only workarounds when a root-cause fix is required.

## 6. Architecture and safety

Preserve the architecture in `docs/architecture.md`. In particular:

- Hive.Core stays dependency-light and host/provider neutral.
- Persistence/database access stays in `Hive.Persistence` or an explicitly authorized persistence boundary.
- `Hive.Management` owns management/application operations.
- `Hive.Host.WinForms` does not bypass `Hive.Management`.
- `Hive.Host.WinForms.UI` owns Hive WinForms presentation.
- `Hive.Example.WinForms` is a consumer/example host, not a platform dependency.
- Use MAF where it already owns the required orchestration mechanism; do not build a second orchestration engine.
- Authorization is enforced in code, never by prompts, UI visibility, or model output.
- Host business state remains host-owned; Hive persistence remains separate.
- Do not expose secrets, private host types, SQL, arbitrary reflection/invocation, or unrestricted host control authority through neutral Hive contracts.
- Before adding an abstraction, identify the existing responsibility owner and reuse it when the contract fits.

## 7. Tests, examples, and UI

`Hive.Tests` is the authoritative automated test project.

Every new meaningful capability needs focused coverage. Every new meaningful externally usable capability also needs a matching `Hive.Example.WinForms` scenario using public contracts and deterministic/reproducible fixtures.

Use `docs/ui/examples.md` for the Example Host pattern. Capability handoff must preserve:

```text
Example to run: <exact Category / Subcategory / optional AdditionalNavigationPath / Example title> — Hive.Example.WinForms
Tests to run: <exact focused test class/file>; broader-suite requirement if applicable
```

For UI/Example changes, follow `docs/ui/` guidance. Reuse existing Hive UI APIs; keep reusable controls free of SQL/provider transport/authorization policy. User-visible failures use `HiveMessageBox` and the Output panel when available.

## 8. Source-of-truth documents

- `docs/Hive_Active_Work.md` is current-state only: keep only the current slice, checkpoint, scope, and verification handoff/state.
- `docs/Hive_Current_Status.md` is current status only.
- `docs/verification/` stores historical verification evidence and must remain auditable; do not overwrite an earlier attempt with a later result.
- `docs/roadmap.md` is the ordered plan, not permission to skip authorization.
- `.agents/skills/` contains reusable workflow procedure and checklists, not current Hive state or architecture.
- Do not document planned behavior as implemented.

When a slice closes, archive historical verification evidence as needed, update Current Status from real evidence, then remove the closed slice from Active Work and leave only the minimal no-active-slice state unless another slice is already authorized.

## 9. Git and final review

Work directly on `main` unless explicitly instructed otherwise. Do not create branches/PRs, rewrite history, force-push, or overwrite unrelated changes unless requested.

Before handoff:

1. review the relevant diff and affected files;
2. confirm scope, architecture, tests/examples/docs, and absence of accidental or stale changes;
3. report exactly what changed, exactly what was verified, and what remains unverified.

## Final rule

**Use repository evidence. Stay inside the authorized slice. Keep scope bounded and engineering depth production-grade. Preserve architecture. Add required tests/examples. Verify only what actually ran. Keep detailed knowledge in the owning docs.**
