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

## 2. Authorization gate

`docs/Hive_Active_Work.md` is the only authority for the current implementation slice.

### Open Active Work

Continue only the authorized slice.

Do not:
- start a later roadmap slice;
- repeat already completed work;
- silently widen scope;
- interpret "continue", "again", "revision", "polish", or "finish" as roadmap advancement.

### Governance/documentation work

An explicitly requested governance, workflow-skill, or source-of-truth documentation task may change only its affected governance/documentation files even when unrelated implementation Active Work is open.

It must not:
- change unrelated implementation;
- close or advance Active Work;
- activate roadmap work.

### Closed or absent Active Work

Stop implementation.

The roadmap defines order, not permission.

A future roadmap slice requires explicit user authorization such as `Hive: Start Phase 1.14` or `Hive: Start the next roadmap slice`.

## 3. Task-context and scope lock

At the start of a task, establish:

- repository and branch;
- checkpoint;
- Active Work item/slice;
- mode and domain;
- affected projects;
- requested scope;
- execution/verification authorization.

For implementation work, Active Work is the maximum authorized implementation boundary.

An explicit user task may narrow that boundary, but does not silently broaden, replace, or advance it.

If the explicit task conflicts with the current Active Work scope, do not choose one interpretation yourself. Report the conflict and require an explicit scope/Active Work change.

`Continue`, `Revision`, `Again`, and similar short follow-ups inherit the immediately preceding task context.

Do not infer a different task from the current Active Work merely because it is open.

For Revision, the immediately preceding task context is authoritative. If it is unavailable or ambiguous, stop before making changes and require an explicit task/context reference.

For Continue after a task has been clearly established, continue that task context. In a fresh context with no prior task, Continue may use an open Active Work item as the implementation context, subject to the authorization gate above.

## 4. Modes

Use the mode requested by the user:

- **Continue** — continue the current task context.
- **Revision** — re-review the work just performed, fix concrete issues within the inherited scope, and repeat the final review.
- **Maintenance** — perform a production audit/correction pass within the authorized maintenance scope.
- **Architecture** — analyze architecture/design; do not implement unless explicitly requested.
- **Verification** — reconcile actual developer verification with repository state.
- **Explicit advancement** — only explicit user authorization moves to another roadmap slice.

Read `references/modes.md` for the command cheat sheet.

## 5. Revision

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

## 6. Maintenance

Maintenance is broader than Revision but remains scope-bound.

Inspect first, identify concrete production problems, correct them, add focused regression coverage when required, inspect affected examples when externally meaningful, and perform a final review.

Use `references/maintenance-checklists.md` for domain-specific review areas.

Maintenance never advances the roadmap.

## 7. Architecture

Architecture mode separates design from implementation.

Distinguish:

- intended architecture;
- current implementation;
- authorized current scope;
- future roadmap possibilities.

Do not implement a design discussed in Architecture mode unless the user explicitly asks to apply it.

## 8. Verification

Verification begins with actual developer-supplied results.

Reconcile requested verification, actual results, current source state, and the Active Work completion gate.

Record only what evidence supports.

Do not convert inspection/reasoning into testing.

Do not close Active Work or activate the next slice without the required real verification and authorization.

## 9. Execution boundary

Follow the execution rules in `AGENTS.md`.

Execution is never implied merely by Continue, Revision, Maintenance, Architecture, or Verification.

When execution is not authorized, do not run builds, tests, launches, migrations, provider calls, or other execution-based verification.

## 10. Final review and handoff

Before implementation handoff or completed Revision:

1. inspect the final diff;
2. confirm scope and architecture;
3. inspect tests/examples/docs;
4. check for accidental, duplicate, stale, or unrelated changes;
5. report exactly what changed;
6. report exactly what was verified;
7. report what remains unverified.

For pending verification, preserve the exact verification handoff required by `AGENTS.md`.

## Hard-stop rules

**No explicit roadmap advancement -> no roadmap advancement.**

**No prior task context -> no guessed Revision/Continue scope.**

**Maintenance -> never becomes roadmap work.**

**Revision -> never becomes roadmap work.**

**"Again" -> same context, not next phase.**

**Completion of one slice -> does not authorize the next slice.**