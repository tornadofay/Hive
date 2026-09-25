# Hive Workflow Modes

## Continue
Continue the current authorized task. Stop at a verification gate.

## Again
Repeat the same task or Revision pass in the same context and scope. Do not create a new task, broaden scope, or advance the roadmap. Stop at a verification gate.

## Revision
Re-audit the work just performed in the same task context. Fix concrete findings to production depth, then review the result again. Revision is corrective, not read-only. When Active Work is open, remain within that authorized implementation boundary. When no Active Work is open and Revision follows a preceding implementation/corrective task, establish a temporary bounded corrective slice before implementation so that task can be re-audited and concrete findings discovered during Revision can be corrected. A read-only Workflow Review is not an implementation/corrective task for this auto-opening rule; Review findings without an existing implementation boundary require explicit bounded corrective authorization. Workflow-documentation Revision stays within the affected governance documents and does not create an implementation slice. The correction must restore/preserve/correct existing behavior; any new capability, material public-contract expansion, or roadmap work requires separate authorization. Revision of an Architecture task remains analysis-only unless implementation is explicitly authorized. A later Revision remains the same scope.

## Maintenance — Backend
Perform a complete production backend/integration audit and correction pass within the authorized boundary. Use the Backend / Integration checklist. When no slice is open, this explicit bounded corrective request may create a temporary slice before implementation if it restores/preserves/corrects existing behavior. New capability or roadmap work still needs explicit authorization. Never use Maintenance to advance the roadmap.

## Maintenance — UI
Perform a complete production WinForms/UI audit and correction pass within the authorized boundary. Use the WinForms / UI checklist. When no slice is open, this explicit bounded corrective request may create a temporary slice before implementation if it restores/preserves/corrects existing behavior. New capability or roadmap work still needs explicit authorization. Never use Maintenance to advance the roadmap.

## Maintenance — Host/UI
Perform a complete production host/UI-boundary audit and correction pass within the authorized boundary. Use the Host / UI Boundary checklist. When no slice is open, this explicit bounded corrective request may create a temporary slice before implementation if it restores/preserves/corrects existing behavior. New capability or roadmap work still needs explicit authorization. Never use Maintenance to advance the roadmap.

## Architecture
Analyze a specific architecture/design question, ownership boundary, contract, or trade-off. Do not implement unless explicitly requested.

## Review (workflow mode)
Perform a **read-only, repository-wide Architecture + Production Engineering review**. This workflow mode is distinct from Hive's V1/future business Review lifecycle/capability. Review may inspect the entire repository, including areas outside Active Work and the current roadmap slice. Inspect docs, architecture, source, project/dependency structure, public contracts, tests, examples, and relevant repository history. Identify defects, incorrect implementations, design/architecture mistakes, duplicated or problematic tests, unnecessary complexity, performance/optimization opportunities, maintainability risks, missing safeguards, and improvements that are larger than normal Maintenance. Recommend better solutions or redesigns with relevant trade-offs and likely scope. Review never modifies code, tests, docs, Active Work, Status, roadmap, or any repository state, and its findings never authorize implementation.

## Verification
Reconcile actual developer results. While Active Work is **VERIFICATION PENDING** and no result has arrived, implementation stops. If verification fails or is partial, classify each result, record **VERIFICATION FAILED / REMEDIATION REQUIRED** before implementation changes, allow same-slice Revision/remediation within that recorded failure boundary, then return Active Work to **VERIFICATION PENDING** with exact rerun targets. Out-of-scope/new-capability work requires separate authorization.

Historical verification evidence is preserved rather than overwritten. Closed slices are removed from Active Work and their historical evidence remains under `docs/verification/`.

## Polish / Polish again
Repeat the same maintenance or polish scope in the same context. Do not create a new task, broaden scope, or advance the roadmap. Stop at a verification gate.

## Context
`Continue` uses the current authorized task; after a task is established it continues that task, and in a fresh context an open Active Work slice may provide the implementation context. `Revision` re-audits the immediately preceding task context and must not guess when that context is unavailable or ambiguous. `Again` repeats the same task or Revision pass; `Polish`/`Polish again` repeats the same maintenance or polish scope. `Again` and `Polish` stop when no applicable task/mode context exists and never create new work. `Revision` may create a temporary corrective slice as defined above. Only explicit roadmap authorization advances the roadmap.
