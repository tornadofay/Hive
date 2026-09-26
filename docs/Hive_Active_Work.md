# Hive — Active Work

Status: VERIFICATION PENDING

## Current slice

**Persistence Resource-Store Deduplication — WorkItem / AgentDefinition**

## Authorization

Bounded corrective maintenance slice. This work removes duplicated scope/access and JSON-metadata implementation by centralizing the shared mechanics in an internal persistence helper used by the existing provider base and the public WorkItem / AgentDefinition stores. The public stores remain public without exposing the internal SQL implementation base as a new API contract.

## Scope

- Refactor `SqlWorkItemResourceStore` and `SqlAgentDefinitionResourceStore` to reuse one internal common helper for the duplicated shared mechanics.
- Keep `SqlResourceStoreBase` internal and make it delegate the same shared mechanics to that helper; do not widen the public API.
- Remove duplicated scope-access predicate and common JSON metadata serialization/deserialization from the two stores where the base contract covers them.
- Preserve WorkItem-specific attachment/event behavior and AgentDefinition-specific persistence semantics.
- Preserve public contracts, persistence schema, authorization behavior, lifecycle/concurrency behavior, cancellation, diagnostics, and existing host/UI behavior.
- Add or adjust focused persistence tests only where needed to prove the refactor preserves behavior.
- No new capability, schema change, roadmap work, or WinForms refactor.

## Checkpoint

Developer verification exposed compile errors in the new common helper and AgentDefinition store. Remediation is limited to restoring the helper dependencies removed during this slice; no contract or schema change is authorized.

Implementation before verification had centralized the duplicated scope-access predicate and JSON metadata serialization/deserialization in one internal helper used by the existing provider base and both affected public stores. The duplicated scope-access predicate and JSON metadata serialization/deserialization now have one internal implementation used by the existing provider base and both affected public stores. No public store type was changed to inherit from the internal SQL base, avoiding a public API expansion.

## Verification gate

A developer verification failure was recorded for missing `GuidParameter` / `IntParameter` in `SqlResourceStoreCommon` and missing `ConcurrencyException` / `IsConstraintConflict` in `SqlAgentDefinitionResourceStore`. Same-slice remediation restored those helper dependencies. The affected files were re-inspected after remediation; no automated build or tests were run by the agent. Developer verification is required before closure.

## Example to run

None — this is an internal persistence maintenance refactor with no new externally usable capability.

## Tests to run

`tests/Hive.Tests/WorkItemManagementTests.cs`; `tests/Hive.Tests/HiveManagementFacadeTests.cs`; then the broader `Hive.Tests` suite.

