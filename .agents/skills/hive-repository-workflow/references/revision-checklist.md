# Hive Revision Checklist

Revision asks whether the work just performed is complete, correct, and still inside the intended boundary.

## Context and scope

- Exact task, mode, domain, and scope
- Active Work authorization, or establishment of a temporary slice from an explicit new bounded non-roadmap corrective task when no slice is open, after confirming the entire request restores/preserves/corrects existing behavior rather than adding capability; confirm the new slice is recorded before implementation
- Respect any developer-verification gate before implementation-affecting changes
- No future roadmap work
- No unrelated behavior changes
- A corrective pass does not expand into newly discovered capability/public-contract work without separate authorization

## User requirement

- Every requested requirement addressed
- Relevant production constraints respected
- Contract-relevant edge cases handled
- Repository evidence used instead of assumptions

## Implementation

- Correctness and invariants
- Nullable/public API contracts
- Validation and structured errors
- Async/cancellation propagation
- Concurrency and stale-result protection
- Lifecycle and state transitions
- Disposal/resource ownership
- Determinism
- Allocation/I/O behavior where contractually relevant

## Architecture

- Correct project owns the responsibility
- Existing owner/extension point reused where appropriate
- No duplicate implementation
- MAF boundary preserved
- Dependency direction preserved
- Public contracts remain appropriate
- No hidden coupling
- No speculative abstraction/dependency

## Security and persistence

When applicable:

- Authorization and scope enforcement
- Secret isolation/redaction
- Parameterized SQL
- Transactions/constraints
- Query efficiency/indexing
- Durable state semantics
- Recovery/idempotency

## Tests and examples

- Changed contracts have appropriate focused coverage
- Regression tests protect real defects
- Tests remain deterministic and isolated
- Required Example Host behavior is present for externally meaningful changes
- Exact Example/Test handoff is preserved when verification is pending

## Documentation and diff

- Correct source-of-truth documents only
- Planned behavior not recorded as implemented
- Complete relevant diff reviewed
- Accidental edits removed
- Duplicate/dead/stale logic checked

## Verification honesty

For every claim ask: did this actually execute, or was it only inspected/reasoned about?

If it did not execute, mark it unverified.
