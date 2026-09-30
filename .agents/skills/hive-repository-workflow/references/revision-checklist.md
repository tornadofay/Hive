# Hive Revision Checklist

Use for Revision and other production re-review passes. It is a quality aid, not permission to widen scope.

## Scope
- original request and acceptance criteria
- Active Work authorization and verification gate
- inherited task/mode/domain and authorized scope
- architecture and roadmap boundary
- no future work or unrelated behavior changes
- Review findings alone do not authorize implementation

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
While Active Work is `VERIFICATION PENDING`, do not begin implementation-affecting work while awaiting developer results. If verification failed/was partial, confirm the failure was recorded before implementation changes. After remediation, return to `VERIFICATION PENDING` and rerun the affected checks.

## Evidence
- preserve earlier verification attempts
- distinguish inspection/reasoning from actual execution
- review the final diff for stale/dead/accidental changes
