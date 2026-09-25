# Hive Workflow Modes

## Continue
Continue the current authorized task. If a fresh context has an open Active Work slice, that slice can provide the task context. Stop at a verification gate.

## Revision
Re-audit the immediately preceding task. Fix concrete problems within the same scope and to production depth, then review the result again. Revision is corrective. It never advances the roadmap or bypasses a verification gate. A Revision after Architecture remains analysis-only; Workflow Review does not itself authorize implementation.

## Again
Repeat the same task or Revision pass in the same context. Do not broaden scope or advance the roadmap.

## Maintenance — Backend
Complete production audit/correction of the authorized backend/integration boundary.

## Maintenance — UI
Complete production audit/correction of the authorized WinForms/UI boundary.

## Maintenance — Host/UI
Complete production audit/correction of the authorized host/UI boundary.

For the three Maintenance modes, an open Active Work slice bounds the work. With no slice, an explicitly requested bounded corrective Maintenance request may create a temporary slice if it only restores, preserves, or corrects existing behavior. New capability or roadmap work requires separate authorization.

## Architecture
Analyze architecture, ownership, contracts, and trade-offs. Do not implement unless explicitly authorized.

## Review
Perform a read-only, repository-wide Architecture + Production Engineering review. It may inspect beyond Active Work and report larger improvements, but it does not modify repository state or create implementation authorization. It is distinct from Hive's V1/future business Review lifecycle.

## Verification
Reconcile actual developer verification results with the current scope. Record in-scope failures before remediation, remediate within the same slice, then return to verification pending. Do not invent verification.

## Explicit roadmap advancement
`Hive: Start Phase X.Y` or `Hive: Start the next roadmap slice` explicitly authorizes roadmap advancement.

## Context
`Continue`, `Again`, and `Polish` stay with the current task/mode. `Revision` stays with the immediately preceding task. If the required context is missing or ambiguous, do not guess. None of these continuation commands advances the roadmap.
