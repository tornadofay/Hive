# Hive AI Agent Operating Rules

This file is the repository operating constitution. Keep it compact. Detailed architecture, APIs, UI guidance, and review checklists belong in their owning documents.

## 1. Authority

These sources have different authoritative roles:

1. `AGENTS.md` — workflow and non-negotiable repository rules.
2. `docs/architecture.md` and relevant `docs/architecture/*.md` — intended architecture and ownership.
3. `docs/Hive_Active_Work.md` — current authorization, implementation slice, and verification gate.
4. `docs/roadmap.md` — ordered future work, not authorization.
5. `docs/plan/` — detailed scope and verification criteria for a roadmap item. Plans are subordinate to the architecture: a conflict means the plan is corrected. A plan never authorizes work by itself.
6. Source/project files — implementation reality.
7. Tests — behavior actually exercised.
8. `docs/Hive_Current_Status.md` — current status only.
9. `docs/verification/` — historical verification evidence.
10. `docs/ui/` and `docs/examples/` — usage guidance.
11. `README.md` — project overview.

`docs/off-work/` holds preserved design and planning notes. It is not an authority and never authorizes implementation; current decisions live in the architecture, roadmap, and plan documents. Preserve its contents unless the user explicitly authorizes deletion.

Do not treat this as a simple precedence chain across different concerns. Apply the source that governs the decision. Previous chat history is not repository authority.

## 2. Before changing code

Read, in order:

1. this file;
2. `docs/Hive_Current_Status.md`;
3. `docs/Hive_Active_Work.md`;
4. the relevant roadmap slice and its linked detailed plan under `docs/plan/`;
5. the relevant architecture sections;
6. relevant UI guidance for UI/Example work.

For cross-project, public-API, persistence, orchestration, lifecycle, security, or durable-state work, read the full applicable architecture documents.

Then inspect affected projects, source, tests, examples, configuration, references, and responsibility owners. Determine the repository checkpoint from evidence.

If Active Work is `VERIFICATION PENDING` before actual developer results arrive, stop implementation-affecting work at that gate. After a real failure is recorded as `VERIFICATION FAILED / REMEDIATION REQUIRED`, same-slice remediation may proceed only within the recorded failure boundary and must return Active Work to `VERIFICATION PENDING`.

## 3. Scope and authorization

Active Work is the maximum implementation boundary.

- `Continue` continues the current authorized task.
- `Revision` re-audits the immediately preceding task and fixes concrete problems within that task's scope.
- `Again` repeats the same task or Revision pass.
- `Polish` / `Polish again` repeats the same maintenance/polish scope.
- `Maintenance — Backend`, `Maintenance — UI`, and `Maintenance — Host/UI` are bounded production audit/correction modes.
- `Repository Review` (repository-wide) and `Workflow Review` (governance documents only) are read-only and never create implementation authorization.
- `Architecture` is analysis/design-only unless implementation is explicitly authorized. It does not change the repository unless the user explicitly requests documentation updates, which follow the documentation rule below.
- `Verification` reconciles actual developer results with the current scope (see section 4).
- None of these commands advances the roadmap or silently widens an implementation boundary.

Terminology:

- A **phase** is a numbered roadmap item such as `1.20` or `1.19A`.
- A **slice** is an internal implementation and verification unit of one phase, recorded in Active Work as `Phase X.Y / Slice N`.
- `Hive: Start Phase X.Y` authorizes that phase and the slice recorded in Active Work. `Hive: Start the next roadmap phase` authorizes only the next roadmap item; the older wording `Hive: Start the next roadmap slice` is an accepted alias.
- `Hive: Start next slice` advances only within the already authorized phase, and only after the current slice is closed. It never crosses into another phase.
- `Continue` never authorizes another slice or phase. No advance is permitted while Active Work is `VERIFICATION PENDING` or `VERIFICATION FAILED / REMEDIATION REQUIRED`.
- ASCII spellings of command names, such as `Maintenance: Backend`, are accepted.

When Active Work is closed or absent, an explicitly requested bounded corrective task may establish a temporary Active Work slice before implementation only when repository evidence confirms that it restores, preserves, or corrects existing behavior without a new capability or material public-contract expansion. A Revision may use this rule only when its immediately preceding task was implementation/corrective work. A Repository Review or Workflow Review does not qualify. Architecture Revision remains analysis-only. Workflow-documentation Revision remains within the governance/documentation files and does not create an implementation slice.

If a corrective pass requires a new capability, material public-contract expansion, future roadmap work, or another scope, stop that portion and require explicit authorization. Mixed corrective + feature requests do not auto-open.

Documentation-only work (architecture, roadmap, plans, UI, examples, workflow documents) is not implementation-affecting. When the user explicitly requests it, it may proceed while Active Work is `VERIFICATION PENDING`, provided it does not change the contract or acceptance criteria being verified. A documentation change that does alter them must be separately authorized, recorded in Active Work, and assessed for whether earlier verification still applies or the affected checks must be rerun. Documentation work never edits Active Work's verification state and never claims verification that did not happen.

A new roadmap phase requires explicit user authorization as defined in the terminology above.

## 4. Verification

Verification is a hard gate between implementation and evidence.

When developer results arrive:

1. required checks pass → complete the normal evidence-backed closure;
2. an in-scope failure/compile error/unmet requirement → record `VERIFICATION FAILED / REMEDIATION REQUIRED` in Active Work before implementation changes;
3. perform same-slice remediation to production depth;
4. return Active Work to `VERIFICATION PENDING` with exact rerun targets;
5. require developer re-verification.

Out-of-scope or new-capability failures require separate authorization.

A **partial** result (some required checks reported, others not) keeps Active Work at `VERIFICATION PENDING` and lists the exact checks still missing. A slice closes only when every required check in the Active Work handoff is reported passing or is explicitly waived by the developer. A waiver is recorded separately in the closure record with its date, the exact waived check, and the reason, and the slice is recorded as `Closed with accepted verification exception`, never as fully verified. A waived check is never recorded as passing.

Verification is developer-run by design at present. Automated CI is planned under roadmap 9.3 and is not a prerequisite for any current slice; it must not be described as existing.

Never claim execution that did not happen. Use precise terms such as Inspected, Reasoned, Compiled, Automated-tested, Integration-tested, Manually verified, and Not verified.

By default, the agent does not run builds, tests, launches, migrations, performance measurements, or external integrations unless explicitly authorized or required by repository workflow.

Hive's root `Directory.Build.props` sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` as standing repository configuration. For routine verification, do not ask the developer to enable or separately confirm this setting. Ask for the actual build result and any reported warnings/errors instead. Revisit the setting only when build-configuration work is explicitly in scope or evidence indicates an override.

## 5. Engineering standard

Keep scope bounded, but make engineering complete and production-grade within the authorized boundary.

Correct concrete root causes and relevant supporting problems involving correctness, contracts/nullability, validation/errors, async/cancellation, concurrency, lifecycle/disposal, determinism/recovery, observability, performance/I/O, authorization/security, persistence, compatibility, tests, examples, and documentation as applicable.

Do not perform unrelated refactoring, speculative abstraction, dependency upgrades without requirement, or symptom-only workarounds.

## 6. Architecture and safety

Preserve `docs/architecture.md` and its detail documents. In particular:

- Hive.Core stays dependency-light and host/provider neutral.
- Persistence/database access stays in `Hive.Persistence` or an explicitly authorized persistence boundary.
- `Hive.Management` owns management/application operations.
- `Hive.Host.WinForms` does not bypass `Hive.Management`.
- `Hive.Host.WinForms.UI` owns Hive WinForms presentation.
- `Hive.Example.WinForms` is a consumer/example host, not a platform dependency.
- Use MAF where it already owns the required orchestration mechanism.
- Authorization is enforced in code, never by prompts, UI visibility, or model output.
- Host business state remains host-owned; Hive persistence remains separate.
- Do not expose secrets, private host types, SQL, arbitrary reflection/invocation, or unrestricted host control authority through neutral Hive contracts.
- Before adding an abstraction, identify the existing responsibility owner and reuse it when the contract fits.

## 7. Tests, examples, and UI

`Hive.Tests` is the authoritative automated test project.

Every new meaningful capability needs focused coverage. Every new meaningful externally usable capability also needs a matching `Hive.Example.WinForms` scenario using public contracts and deterministic/reproducible fixtures.

For UI/Example changes, follow `docs/ui/` guidance and reuse existing Hive UI APIs. User-visible failures use `HiveMessageBox` and the Output panel when available.

Capability handoff must preserve:

```text
Example to run: <exact Category / Subcategory / optional AdditionalNavigationPath / Example title> — Hive.Example.WinForms
Tests to run: <exact focused test class/file>; broader-suite requirement if applicable
```

## 8. Source-of-truth documents

Each document has one job:

- `docs/Hive_Active_Work.md` — current authorization, bounded task, checkpoint, and verification gate. Keep it short; when no task is active, state that plainly.
- `docs/Hive_Current_Status.md` — concise present-day project/phase state and links to important milestones. It is not a chronological diary or a duplicate of the verification archive.
- `docs/verification/` — compact, factual evidence for completed work. Record scope/outcome, meaningful changes, actual developer-reported or tool-run verification, and remaining limitations. Link relevant implementation commits where useful.
- `docs/roadmap.md` — ordered future work, not permission.
- `AGENTS.md` and `.agents/skills/` — stable repository rules and reusable workflows, not a history of individual tasks.
- Architecture, UI, and example documents — durable design decisions and usage guidance in their owning locations.
- Source and tests remain authoritative for what is implemented and exercised. Do not document planned behavior as implemented.
- Architecture documents may label a section `[Implemented]`, `[Target]`, or `[Deferred]` where current and intended behavior are mixed. A label describes implementation status only; verification evidence lives under `docs/verification/`. Update a label only when an authorized change actually affects it.

### Documentation quality rules

- Record information because it helps future implementation, review, operation, or verification—not merely because it happened.
- Prefer a concise result over a full console transcript. Preserve exact output only when it is needed to diagnose a failure, reproduce a behavior, or substantiate an important claim.
- Do not permanently record every typo, temporary compile error, retry, debugging step, or intermediate test count. Retain a resolved failure only when its cause or lesson is likely to prevent recurrence or explains a non-obvious design choice.
- Avoid duplicating the same implementation narrative or verification result across Current Status, Active Work, indexes, and verification records. Use a short summary and a link to the owning record instead.
- Keep one concise closure record for a completed task. Separate attempt/checkpoint records are justified only when they preserve materially useful failure analysis, distinct verification evidence, or an audit that cannot be represented clearly in the closure record.
- Preserve meaningful architectural decisions, compatibility constraints, security/safety findings, and final verification evidence. Do not delete history merely because it is old.
- Before a broad cleanup that deletes, merges, or materially rewrites existing historical documents, list the exact files/sections proposed for removal or consolidation and why. Wait for the user's approval before making those destructive cleanup changes.

When a task closes, update Current Status only if the current state changed, record concise verification evidence under `docs/verification/` when useful, and clear the closed task from Active Work. Do not add a Current Status history entry that merely repeats the verification record; link the record when a milestone materially matters to present-day status.

When adding or removing numbered sections in this file, renumber affected headings and verify there are no duplicate or missing section numbers.

## 9. Git and final review

Work directly on `main` unless explicitly instructed otherwise. Do not create branches/PRs, rewrite history, force-push, or overwrite unrelated changes unless requested.

Before handoff:

1. review the relevant diff and affected files;
2. confirm scope, architecture, tests/examples/docs, and no accidental or stale changes;
3. report exactly what changed, exactly what was verified, and what remains unverified.

## Final rule

**Use repository evidence. Stay inside the authorized slice. Keep scope bounded and engineering depth production-grade. Preserve architecture. Add required tests/examples. Verify only what actually ran. Keep detailed knowledge in the owning docs.**
