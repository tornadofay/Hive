# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.10 — Hive.Management Facade**

Phase 0 — Foundations and Phase 1.1 through Phase 1.9 are complete and verified.

Phase 1.10 is the authorized active implementation slice.

## Objective

Provide the application-facing `Hive.Management` CRUD facade for:

- Providers;
- ProviderAccounts;
- ExecutionTargets;
- AgentDefinitions.

The facade must preserve the existing Core, Agents, Persistence, Provider, and Coordination ownership boundaries. WinForms hosts must consume Management rather than bypassing it.

## Scope

- CRUD operations for Providers;
- CRUD operations for ProviderAccounts;
- CRUD operations for ExecutionTargets;
- CRUD operations for AgentDefinitions;
- service-level validation;
- authorization and ownership/scope enforcement;
- persistence integration through the existing persistence boundaries;
- reuse of existing resource contracts, provider contracts, AgentDefinition/AgentFactory contracts, Result/Error classification, and authorization owners.

## Verification gate

Required for completion of 1.10:

1. Management services expose the required CRUD operations without leaking persistence implementation details;
2. valid create/read/update/delete paths persist and return the expected resources;
3. validation failures are returned as typed Hive errors;
4. authorization, ownership, and scope failures are enforced in code;
5. persistence integration uses the existing Hive.Persistence stores/boundaries;
6. focused automated coverage exists for normal, invalid, authorization, scope, and persistence cases;
7. a public Example Host scenario demonstrates the externally meaningful Management facade behavior;
8. broader `Hive.Tests` execution.

No verification claim is recorded until actual execution has been performed.

## Constraints

- No Phase 1.11 or later implementation.
- No Workspace, Settings, image-processing, business-app integration, or cognitive-generation work.
- Do not move SQL or provider transport ownership into Hive.Management.
- Do not bypass existing authorization/resource contracts.
- Do not duplicate Provider, ProviderAccount, ExecutionTarget, Agent, or persistence state models merely to create facade DTOs unless the existing public contract requires them.
- Host.WinForms remains a consumer boundary; it must not directly access Hive.Persistence for this slice.
- Do not change the existing Provider/ProviderAccount/ExecutionTarget persistence contracts merely to make them fit the Management facade.
- Do not add provider transport behavior to Hive.Management.
- Preserve cancellation, typed errors, resource identity/scope, and existing lifecycle/version semantics.

## Implementation checkpoint

Phase 1.10 has not been implemented yet.

Before coding, inspect:

- the existing provider resource store and contracts;
- AgentDefinition and AgentFactory contracts;
- existing authorization owners and resource-access checks;
- the Hive.Management project/reference boundary;
- existing Example Host patterns;
- existing provider/agent persistence tests and fixtures.

Prefer composition over new domain models. Hive.Management should orchestrate existing owners rather than duplicate their state or persistence rules.

## Implementation handoff

Implemented in the active slice:
- public IHiveManagementFacade / HiveManagementFacade covering Provider, ProviderAccount, ExecutionTarget, and AgentDefinition CRUD;
- Management-boundary validation for deployment/principal identity, resource kind/version/lifecycle, ownership, and scope;
- existing Provider persistence reused unchanged as the authoritative persistence owner for Provider/ProviderAccount/ExecutionTarget;
- durable AgentDefinition identity/resource binding, SQL persistence store, migration, ownership/scope checks, optimistic concurrency, and retirement;
- focused facade/persistence tests covering normal CRUD, validation, owner/scope isolation, concurrency, and retirement;
- public Example Host scenario consuming the Management facade through the Example Host service boundary.

docs/Hive_Current_Status.md remains unchanged because this implementation has not been verified.

## Verification handoff

Example to run: Management / Facade / Hive.Management CRUD Facade — Hive.Example.WinForms

Tests to run: `tests/Hive.Tests/HiveManagementFacadeTests.cs`; broader `Hive.Tests` execution is required by the 1.10 completion gate.

## Historical verification

Completed-slice verification records are maintained under `docs/verification/`. Do not copy completed-slice history into this file.
