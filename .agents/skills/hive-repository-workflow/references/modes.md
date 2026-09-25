# Hive Workflow Modes

## Continue
Continue the current authorized task. Stop at a verification gate.

## Revision
Re-audit the work just performed in the same task context. Fix concrete findings to production depth, then review the result again. Revision is corrective, not read-only. When Active Work is open, remain within that authorized implementation boundary. When no Active Work is open and Revision follows a preceding task with implementation findings, including a Workflow Review, establish a temporary bounded corrective slice before implementation so those findings can be re-audited and corrected. Workflow-documentation Revision stays within the affected governance documents and does not create an implementation slice. The correction must restore/preserve/correct existing behavior; any new capability, material public-contract expansion, or roadmap work requires separate authorization. A later Revision remains the same scope.

## Maintenance
Perform a complete production audit/correction pass within the authorized boundary. When no slice is open, an explicit bounded corrective request may create a temporary slice before implementation if it restores/preserves/corrects existing behavior. New capability or roadmap work still needs explicit authorization. Never use Maintenance to advance the roadmap.

## Architecture
Analyze a specific architecture/design question, ownership boundary, contract, or trade-off. Do not implement unless explicitly requested.

## Review (workflow mode)
Perform a **read-only, repository-wide Architecture + Production Engineering review**. This workflow mode is distinct from Hive's V1/future business Review lifecycle/capability. Review may inspect the entire repository, including areas outside Active Work and the current roadmap slice. Inspect docs, architecture, source, project/dependency structure, public contracts, tests, examples, and relevant repository history. Identify defects, incorrect implementations, design/architecture mistakes, duplicated or problematic tests, unnecessary complexity, performance/optimization opportunities, maintainability risks, missing safeguards, and improvements that are larger than normal Maintenance. Recommend better solutions or redesigns with relevant trade-offs and likely scope. Review never modifies code, tests, docs, Active Work, Status, roadmap, or any repository state, and its findings never authorize implementation.

## Verification
Reconcile actual developer results. While Active Work is **VERIFICATION PENDING** and no result has arrived, implementation stops. If verification fails or is partial, classify each result, record **VERIFICATION FAILED / REMEDIATION REQUIRED** before implementation changes, allow same-slice Revision/remediation within that recorded failure boundary, then return Active Work to **VERIFICATION PENDING** with exact rerun targets. Out-of-scope/new-capability work requires separate authorization.

Historical verification evidence is preserved rather than overwritten. Closed slices are removed from Active Work and their historical evidence remains under `docs/verification/`.

## Context
Continue, Revision, Again, and Polish inherit the immediately preceding task. Do not guess a missing Revision context. Only explicit roadmap authorization advances the roadmap.
