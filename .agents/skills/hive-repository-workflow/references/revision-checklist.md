# Hive Revision Checklist

Use this checklist for Revision and other re-review passes. It is a review aid, not permission to widen scope.

## Scope
- original request and acceptance criteria
- Active Work authorization and verification gate
- architecture and roadmap boundary
- no future work or unrelated behavior changes
- when Revision follows read-only Workflow Review, confirm an existing implementation boundary or explicit bounded corrective authorization; Review findings alone do not authorize implementation

## Production quality
- correctness, invariants, edge cases
- nullability/public contracts
- validation/error behavior
- async/cancellation/concurrency/idempotency
- lifecycle/disposal/resource ownership
- determinism, recovery, stale-state protection
- security/authorization/secrets
- persistence/I/O/performance where relevant
- ownership/dependency direction/duplicate logic
- maintainability and observable failure behavior

## Tests and docs
- focused regression coverage where required
- externally meaningful Example Host behavior
- documentation matches implemented behavior
- exact verification handoff preserved while pending

## Verification remediation
While Active Work is **VERIFICATION PENDING** awaiting developer results, do not begin implementation-affecting work. If developer verification failed/was partial, confirm the failure was classified in Active Work and remediation was recorded before implementation. Revision may then remediate within that recorded failure boundary. After remediation, return to **VERIFICATION PENDING** and rerun the affected checks.

## Evidence
- preserve earlier verification attempts; do not overwrite historical evidence
- distinguish inspection/reasoning from actual execution
- review the final diff for stale/dead/accidental changes
