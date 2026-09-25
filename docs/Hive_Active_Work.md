# Hive — Active Work

## Phase 1.14 — Dual Business-App Integration Contract (Reopened Revision)

Status: VERIFICATION PENDING

Opened: 2026-09-26

### Authorization

Phase 1.14 is explicitly reopened for a bounded revision of the existing verified baseline. This revision does not advance to Phase 1.15 or any later roadmap slice.

### Goal

Extend the existing neutral host-integration contract and WinForms adapter design with a reusable Hive-owned WinForms base form/control layer so host applications can adopt Hive with minimal integration code.

Preferred host experience:

```text
Host application
    ↓
HiveForm / Hive controls
    ↓
automatic safe defaults
    ↓
explicit override only where application semantics require it
    ↓
neutral Hive contract
    ↓
Hive Management authorization
```

The existing non-inheriting-control adapter/semantic-provider path remains supported.

### In scope

- define the ownership and public boundary between neutral contracts, Hive WinForms base types, the compatibility adapter, and host-specific semantics;
- implement the bounded reusable Hive WinForms base form/control layer for justified common controls;
- provide deterministic automatic metadata/capability behavior for standard controls where semantics are safe to infer;
- provide explicit host override points for field/surface identity, generated/computed state, stable row identity, parent/child relationships, lookups, and other application-specific semantics;
- connect automatic/default and explicit-override behavior to the existing neutral contract and Management authorization path;
- preserve support for ordinary/custom/third-party controls through the existing bounded adapter/semantic-provider path;
- extend deterministic reference/example coverage to demonstrate both the low-code base-control path and explicit semantic overrides;
- extend focused tests for defaults, overrides, lifecycle/disposal, stable row identity, generated/computed fields, parent/child surfaces, lookup dependencies, authorization, cancellation, and malformed/unsupported cases;
- update Phase 1.14 architecture/roadmap documentation so completed baseline behavior and this revision scope are clearly distinguished.

### Explicit non-goals

- Phase 1.15 or any later roadmap work;
- consequential host business writes;
- durable business-operation receipts or post-write Review implementation;
- generic UI automation;
- direct host-database/SQL access;
- arbitrary reflection or unrestricted method invocation;
- rebuilding a complete WinForms control toolkit;
- wrapping every WinForms control merely to rename it;
- forcing every host application to inherit from Hive controls;
- reopening Phase 1.13;
- unrelated refactoring, dependency changes, or provider work.

### Required engineering standard

Preserve the existing neutral contract/security boundary. The Hive base layer provides reusable mechanics and safe defaults; the host remains authoritative for business-specific meaning and behavior.

Defaults must be deterministic and explicit overrides must not bypass authorization.

Lifecycle/disposal must never dispose host-owned application objects merely because Hive integration is disposed.


### Implementation checkpoint

The authorized Phase 1.14 revision implementation is complete and has been placed at the verification gate. The change adds the bounded Hive-owned WinForms base form/control layer, deterministic automatic control/surface/capability identities, explicit field/surface semantic overrides, parent/child relationship materialization, and preserves the existing semantic-provider path for custom/ordinary controls. The adapter remains non-owning of host application controls and forms.

### Verification handoff

Example to run: Host / WinForms Integration / Dual Business-App Integration Contract — Hive.Example.WinForms

Tests to run: HiveHostIntegrationContractTests; HiveWinFormsHostIntegrationTests; HiveWinFormsBaseControlIntegrationTests; broader Hive.Tests suite after focused coverage passes.

No build, test, launch, or integration execution has been performed by the agent.
