# Hive — Active Work

Status: VERIFICATION PENDING

Slice: Maintenance — Review Finding Corrections (Five Remaining Production Findings)

Opened: 2026-09-27
Implementation checkpoint: main @ bee92782a6aedd44448b5e786e7ade8068318afe
Implementation completed: main @ d5a47c3c0f2b9526f1e6ea4e55a94ccddc3055f0

## Authorized scope

Correct the five concrete findings from the immediately preceding Workflow Review, without advancing the roadmap or adding new capability:

1. Agent execution lifecycle: handle failure/cancellation while persisting the initial `agent.execution.started` event so the execution cannot be left as an unrepresented in-memory Running execution.
2. Host composition replacement lifecycle: make replacement/disposal failure behavior deterministic and preserve graph ownership/status invariants, including cleanup of the reconfiguration synchronization resource.
3. Database migration cancellation: remove the `Task.Run` masking pattern and provide the existing synchronous DbUp work with a bounded cancellation-aware execution boundary without claiming cancellation can interrupt DbUp itself.
4. DPAPI bootstrap credential file replacement: make concurrent resolution/replacement safe on Windows without exposing plaintext credential material.
5. WorkItem listing scalability: add bounded persistence-side paging while preserving the existing public Management/WorkItem semantics and deterministic ordering.

## Implemented changes

- `AgentExecutionService` now transitions the newly-created execution to a terminal state when initial lifecycle persistence fails or is cancelled, with a best-effort first terminal event so durable lifecycle evidence is retained when possible.
- `HiveHostComposition` now owns the reconfiguration gate lifetime through active-operation accounting; replacement disposal failures no longer turn a successfully published candidate into a failed reload, and the failure is retained as a Ready-state warning.
- `HiveDatabaseMigrator` now uses an explicit long-running scheduler boundary for synchronous DbUp work; caller cancellation remains honored before/after that non-cancellable DbUp segment.
- `DpapiHiveBootstrapCredentialStore` now permits delete-sharing during reads and uses atomic `File.Replace` for existing credential files, with a bounded encrypted-file size.
- Added a bounded keyset-paging contract through Core and Hive.Management; the WinForms Workspace now retrieves WorkItems page-by-page with a stable creation-time/identity cursor.
- Added focused regression coverage for the five findings and aligned the Workspace lifecycle proxy with the paged Management contract.
- Updated `docs/architecture/v1-host-and-management.md` to document the bounded WorkItem listing boundary.

No roadmap advancement, new capability, or unrelated refactoring was introduced.

## Verification gate

Developer must run:
- `tests/Hive.Tests/AgentExecutionIntegrationTests.cs`
- `tests/Hive.Tests/HiveHostCompositionTests.cs`
- `tests/Hive.Tests/HiveBootstrapCredentialStoreTests.cs`
- `tests/Hive.Tests/WorkItemManagementTests.cs`
- `tests/Hive.Tests/HivePersistenceIntegrationTests.cs`
- full `Hive.Tests` suite

No automated test/build/launch result has been claimed by the agent. Until developer results arrive, do not start further implementation-affecting changes.