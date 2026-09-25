---
name: hive-repository-workflow
description: Govern Hive repository work through authorization, scope locking, production-quality review, verification remediation, and safe roadmap handoff.
---

# Hive Repository Workflow

`AGENTS.md` is the constitution. This skill provides the reusable procedure for Hive tasks.

## 1. Bootstrap

Before repository changes:

1. Read `AGENTS.md`.
2. Read Current Status and Active Work.
3. Read the relevant roadmap and architecture sections.
4. Read relevant `docs/ui/` guidance for UI/Example work.
5. Inspect affected source, projects, tests, examples, configuration, and ownership.
6. Establish the current repository checkpoint from evidence.

For revisions of this skill, inspect the complete workflow-skill directory plus `AGENTS.md`.

## 2. Authorization and scope

Active Work defines the current implementation boundary.

If Active Work is `VERIFICATION PENDING` before actual developer results arrive, stop implementation-affecting work. Continue, Revision, Maintenance — Backend, Maintenance — UI, Maintenance — Host/UI, Again, and Polish do not bypass that waiting gate. Once a real failure is recorded as `VERIFICATION FAILED / REMEDIATION REQUIRED`, same-slice Revision/remediation may proceed within the recorded failure boundary; after remediation, return Active Work to `VERIFICATION PENDING` for developer re-verification. Review is read-only, so the waiting gate does not prevent repository-wide inspection.

With no active slice, an explicit non-roadmap corrective request may establish a temporary implementation slice only when it clearly restores/preserves/corrects existing behavior without a new capability or material public-contract expansion. Record that slice before implementation. **Revision is an explicit corrective request in this situation when it follows a preceding implementation task, and may establish the temporary slice needed to re-audit that work and correct concrete findings discovered during Revision.** Workflow-documentation Revision stays within the affected governance documents and does not create an implementation slice. New capabilities and roadmap work require explicit authorization.

Revision inherits the immediately preceding task context, re-audits it for concrete findings, and does not require findings to have been identified in advance. If that preceding task is implementation/corrective work and no Active Work is open, Revision may establish the temporary bounded corrective slice described above before implementation, except for an Architecture task, which remains analysis-only unless implementation is explicitly authorized. A read-only Workflow Review is not an implementation/corrective task for the temporary auto-opening rule; Review findings without an existing implementation boundary require explicit bounded corrective authorization. Workflow-documentation Revision remains governed by the separate governance-documentation rule and does not create an implementation slice. `Continue` uses the current authorized task; in a fresh context, an open Active Work slice may provide that implementation context. `Again` and `Polish` require the applicable established task/mode context and never create new work when none exists. `Revision` requires the immediately preceding task context for its re-audit; if that context is unavailable or ambiguous, do not guess.

Before every write, re-check the branch checkpoint and reconcile any concurrent movement.

## 3. Modes

**Continue** — continue the same authorized task.

**Revision** — re-audit the work just performed, fix concrete in-scope problems to production depth, then review again. Revision is corrective, not read-only. When it follows Workflow Review, use the review findings to make concrete corrections within the applicable authorized boundary; the Review itself does not authorize unrelated work or roadmap advancement.

**Maintenance — Backend** — perform a complete production backend/integration audit and correction pass within the authorized boundary; use the Backend / Integration checklist; do not stop at the first symptom.

**Maintenance — UI** — perform a complete production WinForms/UI audit and correction pass within the authorized boundary; use the WinForms / UI checklist; do not stop at the first symptom.

**Maintenance — Host/UI** — perform a complete production host/UI-boundary audit and correction pass within the authorized boundary; use the Host / UI Boundary checklist; do not stop at the first symptom.

**Architecture** — analyze a specific design/ownership/contract question; do not implement unless explicitly requested.

**Review (workflow mode)** — perform a read-only, repository-wide Architecture + Production Engineering review. This is an agent review mode, not the V1/future business Review lifecycle or capability. Inspect documentation, architecture, implementation, project structure, dependencies, contracts, tests, examples, and relevant repository history. Identify defects, design mistakes, architectural inconsistencies, duplicate or problematic tests, unnecessary complexity, optimization opportunities, maintainability risks, and larger improvements that exceed normal Maintenance scope. Recommend better solutions, including substantial redesigns when warranted. Review findings are advisory only and must not modify code, tests, documentation, Active Work, Status, roadmap, or any other repository state.

**Verification** — reconcile actual developer results with the current scope and completion gate.

**Explicit advancement** — only explicit user authorization moves to another roadmap slice.

**Again** — repeat the same task or Revision pass in the same context and scope; do not reinterpret it as a new task or roadmap advancement.

**Polish / Polish again** — repeat the same maintenance or polish scope in the same context; do not broaden the boundary or advance the roadmap.

None of these modes silently advances the roadmap. Review is read-only and does not create authorization.

## 4. Verification lifecycle

If developer verification fails or is partial:

1. classify each result against Active Work;
2. record `VERIFICATION FAILED / REMEDIATION REQUIRED` in Active Work **before** implementation changes;
3. remediate only within the same authorized slice and to production depth;
4. return Active Work to `VERIFICATION PENDING` with exact rerun targets;
5. require affected developer verification again.

For out-of-scope/new-capability work, stop and require authorization.

When verification closes a slice, archive historical evidence and clean Active Work to current-state-only.

Execution is not implied by any mode. Follow `AGENTS.md` execution rules.

## 5. Production review

When implementation-affecting work is authorized, evaluate the applicable quality areas:

- correctness/invariants/edge cases;
- API/nullability/validation/errors;
- async/cancellation/concurrency/idempotency;
- lifecycle/disposal/resource ownership;
- determinism/recovery/stale-state handling;
- observability/security/authorization;
- persistence/I/O/performance where relevant;
- architecture/ownership/dependency direction;
- tests, regression coverage, examples, and docs.

Read the detailed checklist relevant to the task before changing implementation. Correct root causes and required supporting changes; do not optimize for minimal patch size.

## 6. Governance documentation

Workflow-skill revisions may change workflow/reference documents. They may reconcile Current Status or Active Work only when repository evidence shows a real source-of-truth inconsistency. They must never manufacture authorization, verification, closure, or roadmap advancement.

Active Work and Current Status remain current-state documents. Historical verification belongs in `docs/verification/` and must remain auditable.

## 7. Handoff

Before handoff, review the diff, confirm scope/architecture and required tests/examples/docs, and state exactly what changed, what actually ran, and what remains unverified.

For pending verification, preserve the exact Example/Test handoff required by `AGENTS.md`.
