# Hive — Active Work

Status: VERIFICATION PENDING

## Current slice

**Persistence Resource-Store Deduplication — WorkItem / AgentDefinition**

## Authorization

Bounded corrective maintenance slice. This work removes duplicated scope/access and JSON-metadata implementation by centralizing the shared mechanics in an internal persistence helper used by the existing provider base and the public WorkItem / AgentDefinition stores. The public stores remain public without exposing the internal SQL implementation base as a new API contract.

## Scope

- Refactor `SqlWorkItemResourceStore` to reuse the existing `SqlResourceStoreBase`.
- Refactor `SqlAgentDefinitionResourceStore` to reuse the existing `SqlResourceStoreBase`.
- Generalize provider-specific wording/assumptions in `SqlResourceStoreBase` only where required for safe reuse.
- Remove duplicated scope-access predicate and common JSON metadata serialization/deserialization from the two stores where the base contract covers them.
- Preserve WorkItem-specific attachment/event behavior and AgentDefinition-specific persistence semantics.
- Preserve public contracts, persistence schema, authorization behavior, lifecycle/concurrency behavior, cancellation, diagnostics, and existing host/UI behavior.
- Add or adjust focused persistence tests only where needed to prove the refactor preserves behavior.
- No new capability, schema change, roadmap work, or WinForms refactor.

## Checkpoint

Implementation is complete. The duplicated scope-access predicate and JSON metadata serialization/deserialization now have one internal implementation used by the existing provider base and both affected public stores. No public store type was changed to inherit from the internal SQL base, avoiding a public API expansion.

## Verification gate

Developer verification is required before closure. No automated tests or build have been run by the agent.

## Example to run

None — this is an internal persistence maintenance refactor with no new externally usable capability.

## Tests to run

`tests/Hive.Tests/WorkItemManagementTests.cs`; `tests/Hive.Tests/HiveManagementFacadeTests.cs`; then the broader `Hive.Tests` suite.

