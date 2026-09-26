# Hive — Active Work

Status: IN PROGRESS

Slice: Boundary Implementation Concentration — corrective refactor

## Authorization

Explicitly authorized by the user on 2026-09-26 as a bounded corrective slice to address the three identified large concrete implementation classes:

- `src/Hive.Persistence/Providers/SqlProviderResourceStore.cs`
- `src/Hive.Management/HiveManagementFacade.cs`
- `src/Hive.Host.WinForms.UI/Controls/HiveCrudPage.cs`

## Objective

Reduce implementation-responsibility concentration in those three classes while preserving existing public Hive contracts, behavior, dependency direction, authorization boundaries, persistence semantics, UI behavior, and consumer integration.

## Scope

- Refactor `SqlProviderResourceStore` into resource-specific internal persistence implementations with shared SQL mechanics where appropriate; preserve `IProviderResourceStore`.
- Refactor `HiveManagementFacade` into a thin public facade over internal management responsibility components; preserve `IHiveManagementFacade`.
- Refactor `HiveCrudPage<TItem>` into a thin public reusable UI boundary over internal operation, list/pagination, and layout/theme mechanics where the separation is concrete and justified; preserve its consumer-facing API and existing UI contract.
- Add focused regression coverage for delegation/composition and behavior-preservation boundaries where required.
- Update the owning architecture documentation to record the internal separation model before structural code changes.

## Exclusions

- No roadmap advancement.
- No new externally usable Hive capability.
- No material public contract redesign.
- No new persistence engine, orchestration engine, UI toolkit, or cross-host framework.
- No unrelated cleanup outside the three named implementation boundaries and their directly required tests/docs.
- No changes to Phase 1.15+ authorization.

## Verification boundary

After implementation, the slice must return to `VERIFICATION PENDING` with exact developer rerun targets. No test/build/manual-verification result is recorded until actually performed.

## Checkpoint

Repository checkpoint at slice opening: `b145a760d902a568d2179e6bf3538701e3a5b45c`.
