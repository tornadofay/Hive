# Hive — Active Work

Status: SUPERSEDED — NO ACTIVE IMPLEMENTATION AUTHORIZATION

## Superseded slice

**Phase 1.16 — UI — Provider / Model Capability Discovery Settings Integration**

Checkpoint: `88e45daeec15ca9a7edea629360f0ec49daa2ccd` (main, 2026-09-28)

## Supersession reason

The previously implemented target-first Settings integration is no longer the accepted end-product UI boundary. The revised architecture makes the normal Providers page a simple configured-provider CRUD surface with `Add Provider`, `Refresh`, and a generalized `Advanced` entry point. Automatic ProviderAccount creation and ExecutionTarget reconciliation are Management concerns, while ProviderAccount/ExecutionTarget administration remains available through generalized Advanced Configuration.

The old implementation remains unverified and must not be treated as the final Phase 1.16 UI implementation. Its existing discovery logic may be reused where it fits the revised architecture, but it is not authorization to continue that old target-first UI scope.

## Current architecture handoff

The authoritative revised design is recorded in:
- `docs/architecture/execution-and-persistence.md` — Provider Configuration, Discovery & Target Reconciliation Boundary;
- `docs/architecture/v1-host-and-management.md` — normal Provider Settings versus generalized Advanced Configuration;
- `docs/roadmap.md` — revised Phase 1.16 UI scope and Phase 1.21 Agent execution configuration ownership;
- `docs/ui/forms.md` and `docs/ui/examples.md` — WinForms presentation/Example Host guidance.

No implementation of the revised scope is authorized by this documentation update. An explicit new roadmap-start command is required before implementation resumes.

## Verification state

The superseded implementation was source-reviewed only. No build, test run, application launch, or external provider call was executed by the agent for this documentation revision.
