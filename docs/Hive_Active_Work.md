# Hive — Active Work

Status: VERIFICATION PENDING

## Current slice

**Maintenance — Review Finding Corrections Follow-Up: Agent Start Persistence and Event Persistence Boundary**

The two authorized review corrections are implemented on `main`. This slice remains open until developer verification is reported.

### Completed implementation scope

A. **Agent start-persistence ambiguity/reconciliation**
- Reconciles the original `agent.execution.started` event by EventId after ambiguous initial persistence failure or cancellation.
- Appends the terminal event against the reconciled current stream version.
- Uses a stable terminal EventId so an ambiguous terminal append is reconciled rather than duplicated.
- Removed the empty recovery catch and records bounded recovery failures without replacing the original execution-start failure/cancellation result.

B. **Event persistence public-boundary hardening**
- `IEventPersistenceStore` and `SqlEventPersistenceStore` are internal infrastructure types.
- Added the opaque `HiveEventPersistenceComposition` / `HiveEventPersistence.CreateSql(...)` composition boundary for trusted Hive wiring.
- Updated Host and public Examples to use the hardened boundary instead of raw event-store access.
- Removed the raw Event Persistence Example because its direct API usage would violate the hardened boundary.
- Preserved `Hive.Management` as the application-facing authorization boundary.
- Added focused public-boundary regression coverage.

### Explicit exclusions

- No Phase 1.16+ roadmap work.
- No new event/business capability.
- No changes to Management authorization semantics outside the event-persistence boundary correction.
- No unrelated refactoring, dependency changes, UI changes, provider changes, or schema changes.

## Verification gate

Status: VERIFICATION PENDING

Example to run: `Agents / Base Agent / First Real Agent Execution` — Hive.Example.WinForms
Example to run: `Persistence / Events / Outbox Poller / Transactional Outbox Poller` — Hive.Example.WinForms
Tests to run: `AgentExecutionIntegrationTests`; `EventPersistenceBoundaryTests`; `EventOutboxPollerIntegrationTests`; full `Hive.Tests` suite
Repository build verification: required with Treat Warnings as Errors enabled

No developer verification result is recorded in this document yet.

## Handoff

No externally meaningful new capability is introduced. Verification must confirm existing Agent execution and outbox behavior remains intact while the raw persistence boundary is no longer publicly consumable.
