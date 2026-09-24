# Hive — Active Work

Last updated: 2026-09-25

## Active slice

**Phase 1.14 — Dual Business-App Integration Contract**

Phase 1.14 is now the authorized implementation slice. The preceding Hive.Core Production Polish maintenance pass is closed after developer verification of the final audit revision.

### Scope

- host-neutral Hive.Core public contracts for host registration/adapter ownership;
- semantic control descriptors;
- data-source/data-surface descriptors;
- field/column metadata and bounded value access;
- stable primary/composite/host-defined row identities;
- explicit parent/child data-surface relationships where supplied by the host;
- generated-field and computed-field semantics;
- bounded lookup descriptors and lookup operations;
- bounded host interaction capabilities separate from authorization;
- business-operation capability boundaries sufficient for API-only, UI-only, and API+UI composition;
- concrete bounded WinForms adapter implementation over native/custom WinForms controls;
- application-owned/custom controls through adaptation, without Hive dependency on host libraries;
- Management-owned authorization/orchestration through Core-defined ports;
- provenance, operation correlation, cancellation, lifecycle/disposal, stale-state, and concurrency boundaries;
- focused automated tests for every changed contract and adapter boundary;
- public Example Host scenario proving the neutral contract and concrete WinForms adapter.

### Explicitly out of scope

- consequential business-app writes;
- durable BusinessOperationReceipt / operation-attempt persistence;
- first-class post-write Review;
- input preparation/routing (Phase 1.15);
- structured candidate extraction/validation (Phase 1.16);
- MAF sequential pipeline composition (Phase 1.18);
- generic cross-host technology support (Phase 7);
- direct Hive access to host databases or SQL;
- unrestricted reflection, arbitrary invocation, raw control handles, or model-driven authorization;
- new provider, persistence, orchestration, cognitive-generation, or unrelated UI architecture.

### Required architectural behavior

The neutral contract must preserve the distinction between:

- discovery versus interaction;
- host capability versus Hive authorization;
- UI interaction versus business-operation semantics;
- positional row address versus stable row identity;
- original/pre-operation identity versus resulting identity when a host key changes;
- generated host outputs versus caller-supplied field values;
- computed/read-only fields versus writable fields;
- bounded lookup capability versus arbitrary host query execution;
- API-only, UI-only, and API+UI implementations under one logical operation correlation.

Parent/child relationships must be represented from explicit host-provided relationship metadata rather than inferred from visual nesting, similar names, hidden fields, or filter text.

### Prior slice closure — Hive.Core Production Polish

The Core maintenance pass was completed at commit **4af8a09533a222f75773dca2d5675373cdf1776a**.

The developer supplied the final verification result:

- **216 tests run**
- **216 passed**
- **0 failed**
- **0 skipped**
- **26.4 seconds**
- .NET 10.0.1 / xUnit.net VSTest Adapter 3.1.5

This result verifies the final audit revision. The previous 211-test run was the pre-audit revision and is retained only as historical context.

The maintenance pass changed only Core contracts/tests plus its Active Work documentation. No later roadmap implementation was included.

### Phase 1.14 pre-implementation evidence

Repository architecture establishes that:

- pure host-integration semantics belong in Hive.Core;
- concrete WinForms adaptation belongs in Hive.Host.WinForms;
- Hive.Management owns application-facing orchestration and authorization;
- Management must not reference the concrete WinForms adapter;
- host business/domain state remains owned by the real host application;
- host adapters may use live host objects internally, but those objects must not escape through neutral public contracts;
- no new universal host framework is permitted merely to support the first V1 host.

The repository's prior Phase 1.13 implementation already provides bounded WinForms root registration and immutable discovery snapshots. Phase 1.14 must extend the host boundary rather than duplicate that discovery system.

The documented production-host evidence establishes explicit parent/child data relationships, parent-identity propagation, host validation/veto points, save/reload boundaries, child-data editing, multiple UI edit-surface patterns, generated identities, and positional row selection. The remaining adapter implementation questions are limited to the neutral value/metadata contract, runtime row-identity mapping, generated/computed/edit serialization, bounded lookup execution, host concurrency/version evidence, and any concrete action surface actually required.

### Implementation checkpoint

**Phase 1.14 has been activated in Active Work, but no Phase 1.14 code has been implemented in this handoff yet.**

Starting repository checkpoint:

`4af8a09533a222f75773dca2d5675373cdf1776a`

### Verification handoff

Pending verification must use these exact targets:

Example to run: `Host / WinForms Integration / Dual Business-App Integration` — Hive.Example.WinForms

Tests to run: the focused Phase 1.14 host-integration test file(s) covering Core neutral contracts and WinForms adapter behavior; then `dotnet test tests/Hive.Tests/Hive.Tests.csproj` for the broader suite.

Manual checks must cover:

- neutral contracts contain no WinForms/ORM/SQL/raw-host-object public dependency;
- deterministic fixture adapter demonstrates native/custom control adaptation;
- semantic field and data-surface metadata is exposed without raw control handles;
- parent/child relationships are explicit;
- hidden primary-key identity is available and row index is non-authoritative;
- generated/computed fields have the correct read/write semantics;
- direct-grid, same-form, and dedicated-editor capabilities remain distinct from authorization;
- lookup queries are bounded and cannot execute arbitrary SQL/filters;
- authorization denial is enforced in code even when host UI capabilities are exposed;
- API-only, UI-only, and API+UI paths share one logical operation identity;
- cancellation, disposal, stale-state, concurrency, and provenance behavior are deterministic.

No Phase 1.14 verification has been performed in this handoff.
