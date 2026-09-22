# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.1 — Provider / ProviderAccount / ExecutionTarget**

Phase 0 — Foundations is officially complete. Slice 0.8 was removed from the roadmap before Phase 0 closure because it is no longer needed.

Do not reopen Phase 0 or introduce work from later Phase 1 slices until 1.1 is complete.

## Objective

Establish the first V1 provider-management boundary:

- concrete Provider resources;
- ProviderAccount ownership and scope;
- ExecutionTarget resources;
- three-state capability representation;
- persistence for these resources;
- CRUD-oriented management contracts that later Management UI surfaces can consume.

The implementation must preserve the existing Phase 0 dependency direction, shared UI foundation, typed identity/resource contracts, persistence boundary, and MAF-first architecture.

## 1.1 implementation scope

- Define the Provider / ProviderAccount / ExecutionTarget contracts required by the roadmap and architecture.
- Reuse the existing identity, ownership, scope, lifecycle, version, provenance, error, and persistence infrastructure rather than introducing parallel representations.
- Represent capability support explicitly as Supported / Unsupported / Unknown.
- Establish the persistence boundary and indexed lookup paths required by the contracts.
- Keep provider transport behavior out of Hive.Core and behind the existing provider boundary.
- Keep WinForms presentation separate from management/domain logic; Phase 1 UI consumes the management contracts rather than owning them.
- Preserve the existing shared WinForms foundation in Hive.Host.WinForms.UI and Hive.Example.WinForms; do not reopen Phase 0 UI work unless a concrete Phase 1 requirement exposes a defect in an established contract.

## Verification

Required for completion of 1.1:

1. normal, invalid, and boundary provider/resource contract tests;
2. ownership and scope validation;
3. duplicate-identity and malformed-state cases;
4. persistence integration coverage for create/read/update/delete behavior and relevant indexes;
5. public/API example verification where the new externally meaningful contracts require it;
6. developer verification of any user-facing UI introduced by this slice.

No verification claim is recorded until it has actually been performed.

## Dependency direction

```text
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

## Constraints

- WinForms-specific types remain outside Hive.Core.
- ReaLTaiizor remains exclusively inside Hive.Host.WinForms.UI.
- Hive.Example.WinForms remains a permanent developer-facing project and does not become an alternate test runner.
- Use Microsoft Agent Framework wherever it already owns the required behavior.
- Do not duplicate existing identity, resource, persistence, orchestration, or UI contracts.
- Complete 1.1 before starting 1.2 or later Phase 1 slices.
- Do not start Phase 2 or any cognitive-generation work during this slice.
