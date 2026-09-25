---
name: hive-repository-workflow
description: Govern Hive repository work through authorization, scope control, production-quality review, verification, and safe roadmap handoff.
---

# Hive Repository Workflow

Use this skill for Hive repository work. `AGENTS.md` is the constitution; this skill provides the reusable procedure.

## 1. Bootstrap

Before repository changes:

1. Read `AGENTS.md`.
2. Read Current Status and Active Work.
3. Read the relevant roadmap and architecture sections.
4. Read relevant `docs/ui/` guidance for UI/Example work.
5. Inspect affected source, projects, tests, examples, configuration, references, and responsibility owners.
6. Establish the repository checkpoint from evidence.

For revisions of this skill, inspect the complete workflow-skill directory plus `AGENTS.md`.

## 2. Scope and gates

Active Work is the implementation boundary.

Follow the command meanings in `references/modes.md`. In general:

- implementation-affecting work stays inside Active Work;
- Revision re-audits and corrects the immediately preceding task;
- Maintenance performs a broader bounded production audit in its named domain;
- Review is read-only;
- Architecture does not implement without explicit authorization;
- roadmap advancement requires explicit authorization.

With no Active Work, an explicit bounded corrective request may establish a temporary slice only when it is genuinely corrective. Revision may do so only after an implementation/corrective task. Workflow-documentation work stays in governance documents. New capability, material public-contract expansion, or roadmap work requires explicit authorization.

If Active Work is `VERIFICATION PENDING`, implementation stops. After a recorded `VERIFICATION FAILED / REMEDIATION REQUIRED` state, same-slice remediation may proceed inside the recorded failure boundary, then must return to `VERIFICATION PENDING`.

Before every write, refresh the branch checkpoint if the repository moved.

## 3. Production-quality procedure

For implementation, Revision, and Maintenance:

1. capture the authorized requirement and exclusions;
2. identify the affected ownership boundary;
3. read the applicable reference checklist;
4. inspect implementation, tests, examples, docs, and surrounding ownership;
5. correct concrete root causes and required supporting changes within scope;
6. review the corrected result again.

Evaluate the applicable concerns: correctness, contracts/nullability, validation/errors, async/cancellation/concurrency, lifecycle/disposal, determinism/recovery, security/authorization, persistence/I/O/performance, ownership/dependency direction, maintainability, tests/examples, and documentation.

Do not optimize for minimal patch size or introduce unrelated refactoring/speculative abstraction.

## 4. Verification

Reconcile actual developer results with the authorized task and current source state.

For an in-scope failure:
1. record `VERIFICATION FAILED / REMEDIATION REQUIRED` in Active Work before implementation changes;
2. remediate within the same slice;
3. return to `VERIFICATION PENDING` with exact rerun targets.

Never convert inspection or reasoning into verification. Never invent results.

## 5. Governance documentation

Workflow-skill revisions may update workflow/reference documents. They must remain consistent with `AGENTS.md` and must not manufacture authorization, verification, closure, or roadmap advancement.

Current Status and Active Work remain current-state documents. Historical verification belongs under `docs/verification/`.

## 6. Handoff

Review the final diff and report exactly what changed, what actually ran, and what remains unverified.

For externally meaningful capabilities, preserve the exact Example/Test handoff required by `AGENTS.md`.
