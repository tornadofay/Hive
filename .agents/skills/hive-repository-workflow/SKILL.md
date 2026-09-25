---
name: hive-repository-workflow
description: Govern Hive agent work through repository bootstrap, explicit scope authorization, task-context locking, Continue/Revision/Maintenance/Architecture/Verification modes, and safe roadmap handoff. Use whenever working in the Hive repository.
---

# Hive Repository Workflow

Use this skill for Hive work. It defines workflow procedure; `AGENTS.md` remains the repository constitution and engineering authority.

## 1. Enter through repository authority

Before changing implementation or repository state:

1. Read `AGENTS.md` completely.
2. Read `docs/Hive_Current_Status.md`.
3. Read `docs/Hive_Active_Work.md`.
4. Read the relevant `docs/roadmap.md` section.
5. Read the relevant `docs/architecture.md` sections.
6. Read full `docs/architecture.md` when the task crosses projects, public APIs, persistence, orchestration, lifecycle, security, or durable state.
7. For UI/Example work, read the relevant `docs/ui/` guidance.
8. Inspect affected source/projects/references/tests/examples/configuration and responsibility owners.

Determine the repository checkpoint from evidence. Do not use previous chat history as repository authority.

For a revision of this skill or its references, inspect the complete workflow-skill directory together with `AGENTS.md` so the procedure remains internally consistent. Do not modify implementation or current-state documents unless the user explicitly asks for that separate scope.

## 2. Authorization gate

`docs/Hive_Active_Work.md` is the only authority for the current implementation slice.

### Open Active Work

Continue only the authorized slice.

If the open Active Work item says **VERIFICATION PENDING**, **verification is required**, or otherwise establishes a developer-verification gate, **stop all implementation-affecting work at that gate**. `Continue`, `Revision`, `Maintenance`, `Again`, `Polish again`, and similar follow-ups do not override the gate. A newly worded implementation request also does not silently supersede the gate. Do not resume implementation while the verification gate remains open. Resume implementation only after the developer supplies the required verification results and the repository reflects the authorized task transition. Separately authorized governance/documentation work may proceed only within its explicitly affected documentation scope and must not weaken or remove the verification gate.

Never modify a source-of-truth document merely to manufacture authorization, remove a verification gate, mark work verified, close Active Work, or activate roadmap work. The sole exception relevant here is establishing a new temporary Active Work slice directly from an explicit new bounded implementation task when no slice is open. Any such slice must reflect the user's requested scope and must not activate roadmap work. State changes otherwise require their own authorized task and repository evidence.

Do not:
- start a later roadmap slice;
- repeat already completed work;
- resume implementation past a verification gate;
- silently widen scope;
- interpret "continue", "again", "revision", "polish", or "finish" as roadmap advancement.

### Governance/documentation work

An explicitly requested governance, workflow-skill, or source-of-truth documentation task may change only its affected governance/documentation files even when unrelated implementation Active Work is open.

It must not:
- change unrelated implementation;
- close or advance Active Work;
- activate roadmap work.

### Closed or absent Active Work

An explicitly requested new bounded implementation task may establish a temporary Active Work slice before implementation. Record the new slice's scope from the user request itself, keep it narrowly bounded, and do not include later roadmap work. This is task authorization, not roadmap advancement.

Examples include `Hive: Maintenance — Backend`, `Hive: Maintenance — UI`, `Hive: Maintenance — Host/UI`, or an explicitly scoped implementation request. Generic `Continue`, `Again`, or similar continuation language does not create a new task or slice when no prior authorized task exists. `Revision` does not invent a task when no prior task context exists.

The roadmap defines order, not permission.

A future roadmap slice requires explicit user authorization such as `Hive: Start Phase X.Y` or `Hive: Start the next roadmap slice`.

## 3. Task-context and scope lock

At the start of a task, establish:

- repository and branch;
- checkpoint;
- Active Work item/slice;
- mode and domain;
- affected projects;
- requested scope;
- explicit exclusions or prohibited work;
- execution/verification authorization.

When an Active Work slice exists, it is the maximum authorized implementation boundary for all implementation-affecting work, including Continue, Revision, and Maintenance. The user request selects or narrows the task within that boundary; it does not broaden, replace, or advance Active Work.

When no Active Work slice exists, an explicitly requested bounded implementation task is authorized to establish a new temporary Active Work slice before implementation. The user request defines that new scope; the agent must record it as a bounded temporary slice and must not turn it into roadmap work.

For an explicitly authorized governance/documentation task, the named affected governance/documentation files are the change boundary, subject to the governance exception in the authorization gate.

An explicit user task may narrow an existing authorized implementation boundary, but does not silently broaden, replace, or advance it.

If the explicit task conflicts with the current Active Work scope, do not choose one interpretation yourself. Report the conflict and require an explicit scope/Active Work change.

`Continue`, `Revision`, `Again`, and similar short follow-ups inherit the immediately preceding task context.

Do not infer a different task from the current Active Work merely because it is open.

For Revision, the immediately preceding task context is authoritative. If it is unavailable or ambiguous, stop before making changes and require an explicit task/context reference.

For Continue after a task has been clearly established, continue that task context. In a fresh context with no prior task, Continue may use an open Active Work item as the implementation context, subject to the authorization gate above.

Before any repository write, re-check the current branch checkpoint. If the branch moved since the checkpoint used for inspection, refresh and reconcile against the new tip before writing. Never overwrite or force-resolve concurrent changes you did not create.

## 4. Modes

Use the mode requested by the user:

- **Continue** — continue the current task context.
- **Revision** — re-review the work just performed, fix concrete issues within the inherited scope, and repeat the final review.
- **Maintenance** — perform a production audit/correction pass within the authorized maintenance scope.
- **Architecture** — analyze architecture/design; do not implement unless explicitly requested.
- **Verification** — reconcile actual developer verification with repository state.
- **Explicit advancement** — only explicit user authorization moves to another roadmap slice.

Read `references/modes.md` for the command cheat sheet.

## 5. Production quality lens

Once the authorization and verification gates permit implementation, every production implementation, Revision, or Maintenance pass must actively evaluate the quality areas that apply to the changed boundary. Do not treat this as a generic checklist pass; inspect the concrete code, contracts, lifecycle, and surrounding ownership.

### Always applicable

- correctness, invariants, and edge cases;
- public/nullability/API contract correctness;
- validation, clear structured errors, and failure behavior;
- async behavior, cancellation propagation, and post-await safety;
- concurrency, supersession, stale-result protection, and idempotency where relevant;
- lifecycle, state transitions, disposal, and resource ownership;
- deterministic behavior and safe recovery/failure isolation;
- diagnostics, logging/telemetry, and error observability without leaking secrets or sensitive data;
- performance, allocation, I/O, timeout/budget, and resource-use behavior where contractually relevant;
- correct project/owner, dependency direction, and reuse of existing responsibilities;
- no duplicate implementation, hidden coupling, speculative abstraction, or unrelated behavior change;
- security, authorization/ownership/scope, credential/secret isolation, and safe error disclosure where relevant;
- persistence, transaction/constraint semantics, query efficiency, and durable-state correctness where relevant;
- serialization/public-contract compatibility and extensibility where required;
- required focused tests, regression coverage, and externally meaningful Example Host behavior;
- final diff, accidental/stale/dead changes, and documentation consistency.

### Backend and integration boundaries

Also inspect network/database/provider I/O, timeouts/budgets, configuration/effective configuration, transaction boundaries, recovery/reconciliation, event/lifecycle semantics, and the MAF responsibility boundary where applicable.

### WinForms/UI and Host/UI boundaries

Also inspect visual hierarchy/readability, spacing/alignment/density, theme consistency, selected/hover/pressed/focused/disabled/read-only states, keyboard/focus behavior, validation/loading/empty/success/error states, resizing/anchoring/docking, minimum sizes/overflow, DPI/scaling, responsiveness, thread affinity, dialog ownership, host composition/lifetime, UI-thread constraints, bounded discovery/interaction authority, and Example Host use of public contracts.

### Required quality workflow

When the authorization and verification gates permit implementation-affecting work:

1. Capture the user request as explicit acceptance criteria, scope boundaries, exclusions, and verification requirements before implementation. Do not invent requirements or omit stated ones.
2. Identify the changed boundary and its responsibility owner.
3. Read the applicable detailed reference checklist **before** making implementation changes or concluding the audit. Load only the reference material relevant to the actual task and boundary:
   - Continue/implementation → read `references/revision-checklist.md` and only the domain sections of `references/maintenance-checklists.md` matching the authorized affected projects/boundary;
   - Revision → read `references/revision-checklist.md` plus only the applicable domain section(s) of `references/maintenance-checklists.md`;
   - Maintenance — Backend → use the Backend section of `references/maintenance-checklists.md`;
   - Maintenance — UI → use the WinForms/UI section of `references/maintenance-checklists.md`;
   - Maintenance — Host/UI → use the Host/UI section of `references/maintenance-checklists.md`;
   - Backend/integration work → use the corresponding Backend/Boundaries sections;
   - UI/Host/UI work → use the corresponding WinForms/UI and/or Host/UI sections.
   - If the authorized task spans multiple domains, load each affected domain section and no unrelated sections.
   Do not load unrelated detailed checklists merely because they exist.
4. Evaluate the actual implementation and surrounding code against the main quality lens and only the applicable detailed checklist(s). Do not treat either as a box-counting exercise.
5. Correct every concrete issue within the authorized scope, while preserving existing behavior unless the active contract requires a change.
6. Re-review the corrected result against the same quality criteria and acceptance criteria before handoff.

A short command such as `Hive: Continue`, `Hive: Revision`, or `Hive: Maintenance` does not reduce these quality requirements. The quality lens and applicable detailed reference remain mandatory whenever implementation-affecting work is authorized.

These references expand the quality lens; they do not change authorization, scope, or roadmap permission.

## 6. Revision

Revision is not a new task.

Review the final result against:

- the original request;
- inherited scope and Active Work;
- `AGENTS.md`;
- architecture and relevant roadmap boundaries;
- affected implementation and complete relevant diff;
- tests/examples/documentation;
- lifecycle, concurrency, cancellation, security, persistence, UI, or other boundary concerns that apply.

Fix every concrete issue found within the inherited scope.

Then review the corrected result again.

A repeated Revision remains in the same context.

Read `references/revision-checklist.md` for the detailed checklist.

## 7. Maintenance

Maintenance is broader than Revision but remains scope-bound.

Inspect first, identify concrete production problems, correct them, add focused regression coverage when required, inspect affected examples when externally meaningful, and perform a final review.

Use `references/maintenance-checklists.md` for domain-specific review areas.

Maintenance never advances the roadmap.

## 8. Architecture

Architecture mode separates design from implementation.

Distinguish:

- intended architecture;
- current implementation;
- authorized current scope;
- future roadmap possibilities.

Do not implement a design discussed in Architecture mode unless the user explicitly asks to apply it.

## 9. Verification

Verification begins with actual developer-supplied results.

Reconcile requested verification, actual results, current source state, and the Active Work completion gate.

Record only what evidence supports.

Do not convert inspection/reasoning into testing.

Do not close Active Work or activate the next slice without the required real verification and authorization.

## 10. Execution boundary

Follow the execution rules in `AGENTS.md`.

Execution is never implied merely by Continue, Revision, Maintenance, Architecture, or Verification.

When execution is not authorized, do not run builds, tests, launches, migrations, provider calls, or other execution-based verification.

## 11. Final review and handoff

Before implementation handoff or completed Revision:

1. inspect the final diff;
2. confirm scope and architecture;
3. inspect tests/examples/docs;
4. check for accidental, duplicate, stale, or unrelated changes;
5. report exactly what changed;
6. report exactly what was verified;
7. report what remains unverified.

For pending verification, preserve the exact verification handoff required by `AGENTS.md`.

For governance/workflow-skill changes, also verify that the edited procedure does not contradict `AGENTS.md`, silently alter current Hive state, or introduce a stale roadmap-specific command/example.

## Hard-stop rules

**No explicit roadmap advancement -> no roadmap advancement.**

**No prior task context -> no guessed Revision/Continue scope.**

**Maintenance -> never becomes roadmap work.**

**Revision -> never becomes roadmap work.**

**"Again" -> same context, not next phase.**

**Completion of one slice -> does not authorize the next slice.**

**A moved repository checkpoint -> refresh before writing.**
