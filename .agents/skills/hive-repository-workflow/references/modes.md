# Hive Workflow Modes

## Continue
Continue the current authorized task. Stop at a verification gate.

## Revision
Re-review the work just performed in the same task context. Fix concrete in-scope problems to production depth. Never advance the roadmap. A later Revision remains the same scope.

## Maintenance
Perform a complete production audit/correction pass within the authorized boundary. When no slice is open, an explicit bounded corrective request may create a temporary slice before implementation if it restores/preserves/corrects existing behavior. New capability or roadmap work still needs explicit authorization. Never use Maintenance to advance the roadmap.

## Architecture
Analyze a specific architecture/design question, ownership boundary, contract, or trade-off. Do not implement unless explicitly requested.

## Review
Perform a **read-only, repository-wide Architecture + Production Engineering review**. Review may inspect the entire repository, including areas outside Active Work and the current roadmap slice. Inspect docs, architecture, source, project/dependency structure, public contracts, tests, examples, and relevant repository history. Identify defects, incorrect implementations, design/architecture mistakes, duplicated or problematic tests, unnecessary complexity, performance/optimization opportunities, maintainability risks, missing safeguards, and improvements that are larger than normal Maintenance. Recommend better solutions or redesigns with relevant trade-offs and likely scope. Review never modifies code, tests, docs, Active Work, Status, roadmap, or any repository state, and its findings never authorize implementation.

## Verification
Reconcile actual developer results. If verification fails or is partial, classify each result, record **VERIFICATION FAILED / REMEDIATION REQUIRED** before implementation changes, remediate within the same slice, then return Active Work to **VERIFICATION PENDING** with exact rerun targets. Out-of-scope/new-capability work requires separate authorization.

Historical verification evidence is preserved rather than overwritten. Closed slices are removed from Active Work and their historical evidence remains under `docs/verification/`.

## Context
Continue, Revision, Again, and Polish inherit the immediately preceding task. Do not guess a missing Revision context. Only explicit roadmap authorization advances the roadmap.
