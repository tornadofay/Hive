# Hive Workflow Modes

`AGENTS.md` section 3 owns the authorization rules and terminology; this file holds the full mode definitions. ASCII spellings such as `Maintenance: Backend` are accepted.

## Continue
Continue the current authorized task. If a fresh context has an open Active Work slice, that slice can provide the task context. Stop at a verification gate.

## Revision
Re-audit the immediately preceding task. Fix concrete problems within the same scope and to production depth, then review the result again. Revision is corrective. It never advances the roadmap or bypasses a verification gate. A Revision after Architecture remains analysis-only; Repository Review and Workflow Review do not themselves authorize implementation.

## Again
Repeat the same task or Revision pass in the same context. Do not broaden scope or advance the roadmap.

## Polish
Repeat the same maintenance/polish scope in the same context (`Polish again`). Do not broaden scope or advance the roadmap.

## Maintenance — Backend
Complete production audit/correction of the authorized backend/integration boundary.

## Maintenance — UI
Complete production audit/correction of the authorized WinForms/UI boundary.

## Maintenance — Host/UI
Complete production audit/correction of the authorized host/UI boundary.

For the three Maintenance modes, an open Active Work slice bounds the work. With no slice, an explicitly requested bounded corrective Maintenance request may create a temporary slice if it only restores, preserves, or corrects existing behavior. New capability or roadmap work requires separate authorization.

## Architecture
Analyze architecture, ownership, contracts, and trade-offs. Do not implement unless explicitly authorized. Documentation updates it recommends are made only when the user explicitly requests them, and follow the documentation-only rule in `AGENTS.md` section 3.

## Repository Review
The former `Review` name is an accepted alias. Perform a read-only, repository-wide Architecture + Production Engineering review. It may inspect beyond Active Work and report larger improvements, but it does not modify repository state or create implementation authorization. It is deliberately not called plain Review so it is not confused with Hive's V1/future first-class business Review lifecycle.

## Workflow Review
Perform a read-only review of the governance documents only: `AGENTS.md`, the complete workflow skill directory, and its references. It does not modify repository state and never creates implementation authorization.

## Verification
Reconcile actual developer verification results with the current scope. Record in-scope failures before remediation, remediate within the same slice, then return to verification pending. Do not invent verification. A partial result keeps the slice `VERIFICATION PENDING` with the missing checks listed. A waived check is recorded separately and the slice is closed as `Closed with accepted verification exception`, never as fully verified (see `AGENTS.md` section 4).

## Explicit advancement
`Hive: Start Phase X.Y` or `Hive: Start the next roadmap phase` (alias: `Hive: Start the next roadmap slice`) authorizes a roadmap phase. `Hive: Start next slice` advances only within the already authorized phase after the current slice is closed. See `AGENTS.md` section 3 for the terminology and limits.

## Context
`Continue`, `Again`, and `Polish` stay with the current task/mode. `Revision` stays with the immediately preceding task. If the required context is missing or ambiguous, do not guess. None of these continuation commands advances the roadmap.
