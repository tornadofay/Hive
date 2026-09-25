# Hive Workflow Modes

## Continue
Continue the current authorized task. Stop at a verification gate.

## Revision
Re-review the work just performed in the same task context. Fix concrete in-scope problems to production depth. Never advance the roadmap. A later Revision remains the same scope.

## Maintenance
Perform a complete production audit/correction pass within the authorized boundary. When no slice is open, an explicit bounded corrective request may create a temporary slice before implementation if it restores/preserves/corrects existing behavior. New capability or roadmap work still needs explicit authorization. Never use Maintenance to advance the roadmap.

## Architecture
Analyze architecture, ownership, contracts, and trade-offs. Do not implement unless explicitly requested.

## Verification
Reconcile actual developer results. If verification fails or is partial, classify each result, record **VERIFICATION FAILED / REMEDIATION REQUIRED** before implementation changes, remediate within the same slice, then return Active Work to **VERIFICATION PENDING** with exact rerun targets. Out-of-scope/new-capability work requires separate authorization.

Historical verification evidence is preserved rather than overwritten. Closed slices are removed from Active Work and their historical evidence remains under `docs/verification/`.

## Context
Continue, Revision, Again, and Polish inherit the immediately preceding task. Do not guess a missing Revision context. Only explicit roadmap authorization advances the roadmap.
