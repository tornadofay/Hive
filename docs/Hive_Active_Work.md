# Hive — Active Work

Last updated: 2026-09-21

## Active slice

**0.3 — Identity, WorkItem & Resource foundation**

Phase 0.2 is complete. Do not begin 0.4 or any later slice until 0.3 is complete and its verification has actually been performed.

## Objective

Implement the common identity and resource foundation required by all later Hive capabilities:

- Deployment/Tenant/Principal/User/Session/Workspace/Agent/Hive/Runtime/Execution/WorkItem identities;
- explicit Resource envelope with owner and canonical scope;
- immutable provenance and resource references;
- positive resource versioning;
- resource lifecycle metadata and controlled lifecycle transitions;
- scope matching with fail-closed missing identity handling;
- immutable identity snapshots and metadata isolation;
- WorkItem identity independent from Runtime/Execution lifetime;
- WorkItem lifecycle/status transitions with versioned state;
- copyable public API example.

## Implementation progress

The 0.3 implementation is committed to `Hive.Core`:

- strongly typed non-empty identities for all required resource/runtime/work identifiers;
- `ResourceKind` inventory classification;
- `ResourceScope` and `ResourceAccessContext` with the documented Global/Tenant/User/Workspace/Agent/Runtime/Execution scope matrix;
- fail-closed scope matching when required deployment, tenant, principal, agent, runtime, user, workspace, or execution identity is absent;
- `ResourceVersion` with monotonic advancement and overflow protection;
- `ResourceLifecycle` with Active/Suspended/Retired transitions and retirement protection;
- `ResourceProvenance` with creator principal, creation time, correlation/causation IDs, and optional source reference;
- immutable `ResourceEnvelope<TIdentity>` with copied metadata and identity snapshots;
- `WorkItem` with creation, queue/run/pending-approval/completed/rejected/failed/cancelled statuses, suspension/resume, retirement, immutable identity preservation, and versioned transitions.

A copyable public-API example is in `docs/examples/Phase03_Identity_Resource.md`.

## 0.2 completion record

Phase 0.2 was completed after the developer pulled the fixes, rebuilt the full solution, launched the application successfully, and ran the complete test suite.

- Build result: **PASS — developer reports full-solution rebuild succeeds.**
- Runtime result: **PASS — developer reports the solution runs successfully.**
- Test result: **PASS — 20 tests, 20 passed, 0 failed, 0 skipped, 1.3 seconds.**
- Verification result: **PASS for the 0.2 completion gate based on the developer-run test suite.**
- Ready commit before starting 0.3: `0381a2fc75ebf0aeb2020d62b970d46124d455fe`
- Next slice: **0.3 — Identity, WorkItem & Resource foundation**

## Dependency direction

```
Hive.Core
   ↑
Agents / Persistence / Tools / Providers
   ↑
Management
   ↑
Host.WinForms
   ↑
Host.WinForms.UI

Example.WinForms → public platform contracts + Host.WinForms + Host.WinForms.UI
Tests → projects under test

Coordination may depend on Core + Agents + MAF contracts where required.
No core/platform project may depend on Example.WinForms.
```

Additional constraints:

- WinForms-specific types remain outside Hive.Core.
- Provider transport remains outside Hive.Core.
- The ReaLTaiizor package is **not introduced in 0.1**; it is added and verified in 0.6, and only Hive.Host.WinForms.UI may reference it.
- Hive.Example.WinForms must not reference xUnit runner internals.
- Do not create compatibility/legacy projects or duplicate architecture paths.

## Out of scope for 0.3

- persistence schema or database migrations;
- provider implementation;
- Agent/Hive behavior beyond identity contracts;
- authorization/permission policy beyond explicit scope matching;
- database-backed WorkItem persistence;
- execution orchestration;
- UI foundation or Example host navigation;
- V1 image/document pipeline;
- CognitiveAgent/CognitiveHive;
- later persistence/outbox behavior.

## 0.3 Verification

The implementation requires verification of:

1. all eleven typed identity contracts reject empty values and support stable round-tripping;
2. the complete scope matrix matches when all required identity components are present;
3. each scope fails closed for missing required identity components;
4. wrong tenant/user/workspace/agent/runtime/execution identity does not match;
5. resource metadata is copied so external mutation cannot alter the resource;
6. identity snapshots remain stable when a resource is versioned;
7. resource versions are positive, monotonic, and overflow-safe;
8. retired resource lifecycle cannot be reactivated;
9. WorkItem transitions preserve identity, owner, scope, and provenance while incrementing version and updating lifecycle time;
10. terminal WorkItems cannot be transitioned;
11. suspended WorkItems can resume without losing work status;
12. separate WorkItems do not share mutable state;
13. the public example remains consistent with the actual public API.

Automated execution is currently pending. No 0.3 pass claim is recorded yet.

## Completion record

Complete this section after verification:

- Build result:
- Verification result:
- Commit:
- Next slice:
