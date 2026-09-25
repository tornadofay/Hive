---
name: hive-repository-workflow
description: Govern all AI-agent work in the Hive repository using its repository authority, Active Work authorization gate, task-context lock, Continue/Revision/Maintenance/Architecture/Verification modes, production review, and verification handoff. Use whenever an agent works on Hive.
---

# Hive Repository Workflow

Use this skill for all Hive repository work.

## Core principle

The repository is the source of truth.

This skill defines reusable workflow procedure. It does not replace or duplicate Hive's current architecture, roadmap, implementation state, or verification history.

Always start by reading AGENTS.md. Follow its authority order and task-specific reading requirements.

## Entry and authorization gate

Before implementation or repository-changing work:

1. Read AGENTS.md completely.
2. Read docs/Hive_Current_Status.md.
3. Read docs/Hive_Active_Work.md.
4. Read the relevant docs/roadmap.md section.
5. Read the relevant docs/architecture.md sections.
6. Read full docs/architecture.md when the task crosses projects, public APIs, persistence, orchestration, lifecycle, security, or durable state.
7. For UI/Example work, read the relevant docs/ui/ guidance.
8. Inspect the affected implementation, project references, tests, examples, configuration, and responsibility owners before changing anything.

Determine the current repository checkpoint from evidence.

### Open Active Work

When docs/Hive_Active_Work.md contains an open slice or maintenance pass:

- continue only that authorized work;
- preserve its scope and verification gate;
- do not start a later roadmap slice;
- do not interpret "continue", "again", "revise", "polish", or "finish" as roadmap advancement.

### Governance/documentation exception

An explicitly requested governance, workflow-skill, or source-of-truth documentation task may modify only its affected governance/documentation files even when an unrelated implementation slice is open.

Such work must not:
- modify unrelated implementation;
- close or advance Active Work;
- activate a roadmap slice;
- treat the governance task as a substitute for implementation of the active slice.

### Closed or absent Active Work

A closed or absent Active Work item is a stop boundary for implementation.

Do not automatically activate the next roadmap slice.

The roadmap describes order, not permission.

Only an explicit user instruction to advance the roadmap can authorize it, for example:

- Hive: Start Phase 1.14
- Hive: Start the next roadmap slice
- Hive: Move to Phase 1.14

If the user has not explicitly advanced the roadmap, report the state and stop rather than inventing new implementation scope.

## Task-context lock

When a task starts, treat these as the working context:

- repository;
- branch;
- repository checkpoint;
- Active Work item;
- authorized slice;
- mode;
- domain;
- affected projects;
- requested scope;
- verification authorization.

Subsequent Revision, Again, or Continue messages inherit that context unless the user explicitly changes it.

Never broaden a later message into a new roadmap slice merely because the previous work appears complete.

If a short command such as Revision arrives without enough prior-task context, do not infer a task from the current Active Work merely because it is the only open item. Stop before making changes and require an explicit task/context reference in the user instruction.

## Modes

Use the mode requested by the user. Load references/modes.md when the mode is ambiguous or when detailed behavior is needed.

- Continue — continue the current authorized work.
- Revision — re-check the work just completed, fix concrete issues within the inherited scope, and repeat the final review.
- Maintenance — perform a production audit and correction pass within the authorized maintenance scope.
- Architecture — reason about architecture and documentation; do not implement unless implementation is explicitly requested.
- Verification — reconcile actual developer verification results with repository state and update the owning records from evidence.
- Explicit advancement — only an explicit user instruction changes the roadmap slice.

Do not silently convert one mode into another.

## Implementation and repository changes

Follow AGENTS.md for production engineering, architecture, tests, examples, documentation ownership, and Git rules.

Use the smallest correct change that satisfies the authorized task.

Prefer existing responsibility owners, contracts, extension points, persistence boundaries, MAF mechanisms, UI patterns, and dependencies.

Do not add speculative abstractions, duplicate implementations, unrelated cleanup, dependency upgrades, framework replacements, or hidden architectural changes.

Do not weaken validation, authorization, cancellation, security, or failure visibility.

Do not duplicate current status or architectural truth into skill files.

## Verification boundary

Execution is not implied by this skill.

Unless the user explicitly authorizes execution under the repository rules, do not run:

- builds;
- tests;
- application launches;
- migrations;
- provider/network calls;
- performance measurements;
- other execution-based verification.

Use precise evidence terms:

- Inspected
- Reasoned
- Compiled
- Automated-tested
- Integration-tested
- Manually verified
- Not verified

Never claim execution that did not occur.

## Revision behavior

Revision is a quality-control pass over the work just performed.

A Revision must:

1. identify the inherited task context;
2. review the final implementation, not only the original code;
3. compare the result against the user request;
4. compare it against AGENTS.md, Active Work, architecture, roadmap scope, and relevant UI/implementation guidance;
5. inspect the complete relevant diff;
6. look for omissions, incorrect behavior, regressions, inconsistent contracts, lifecycle/cancellation/concurrency problems, security or persistence mistakes, duplicated responsibility, stale/dead code, missing test/example coverage, and accidental scope expansion;
7. correct every concrete issue found that belongs to the inherited scope;
8. review the corrected result again;
9. preserve the existing verification gate.

Revision is allowed to change code or owning documentation when a concrete issue is found.

Revision is never permission to start future work.

Repeated Revision requests remain in the same context until the user explicitly changes it.

Load references/revision-checklist.md for the detailed Revision checklist.

## Maintenance behavior

Maintenance is broader than Revision but still bounded.

A maintenance request should:

1. inspect first;
2. identify concrete production defects or quality gaps;
3. correct only issues inside the authorized maintenance scope;
4. add focused regression coverage when a discovered defect protects a real contract;
5. inspect affected examples when public behavior changes;
6. review the final implementation again;
7. stop at the verification gate unless verification is explicitly supplied or authorized.

Use references/maintenance-checklists.md for backend, WinForms/UI, and Host/UI boundary review areas.

Maintenance never advances the roadmap.

## Architecture behavior

Architecture mode is for design and source-of-truth reasoning.

Inspect the current architecture and actual implementation as necessary.

Distinguish:

- intended architecture;
- implementation reality;
- current authorized scope;
- future roadmap possibilities.

Do not implement a proposed architectural change merely because the discussion identified it.

When the user explicitly asks to apply the architectural decision, switch to implementation only within the newly authorized scope.

## Verification reconciliation

Verification mode starts from the developer's actual supplied results.

Reconcile:

- requested verification;
- actual results;
- failures or omissions;
- current source state;
- Active Work verification gate;
- historical verification requirements.

Record only what the evidence supports.

Do not upgrade "inspected" or "reasoned" into "tested".

Do not close Active Work until its completion gate is actually satisfied.

## Final review

Before every implementation handoff or completed revision:

1. inspect the final diff;
2. confirm scope remained inside the authorized context;
3. confirm architecture and responsibility boundaries;
4. inspect changed tests and examples;
5. check for duplicate/dead/stale logic;
6. verify documentation changes belong to the owning source-of-truth location;
7. state exactly what changed;
8. state exactly what was verified;
9. state what remains unverified.

## Hard-stop rules

- No explicit roadmap advancement -> no roadmap advancement.
- No active slice -> no automatic implementation.
- Maintenance -> never becomes roadmap work.
- Revision -> never becomes roadmap work.
- "Again" -> never means next phase.
- "Continue" -> same authorized context unless the user explicitly changes it.
- Completion of one slice -> does not authorize the next slice.
