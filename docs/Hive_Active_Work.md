# Hive — Active Work

Last updated: 2026-09-23

## Active slice

**1.13 — Image Input & WinForms Host Context**

### Current sub-stage

**1.13-A — WinForms Host Context Discovery**

Phase 1.12 is complete and verified. Phase 1.13 is the authorized implementation slice.

## Objective

Establish image as the first V1 input boundary and provide the concrete WinForms host-context discovery contract needed by later bounded UI integration.

## Scope

- checked-in image fixture usable by deterministic tests/examples;
- bounded WinForms root registration/discovery;
- Form/UserControl/custom Control/container/nested descendant discovery;
- relevant read-only structural/runtime context;
- cycle-safe and bounded traversal;
- cancellation-aware traversal;
- explicit provenance for discovered host context;
- no control mutation/action authority;
- focused automated coverage and a public Example Host scenario.

Do not implement Phase 1.14 or later work in this run.

## Architectural constraints

- Preserve Hive.Core host neutrality; WinForms-specific discovery belongs in the host integration boundary.
- Do not create a second host context system or duplicate WorkItem image-storage contracts.
- Discovery is contextual/read-oriented only. It never grants permission to click, edit, invoke, or mutate controls.
- Traversal must remain bounded, cycle-safe, cancellation-aware, and deterministic.
- Host registrations and discovered snapshots must have explicit ownership/disposal semantics.
- Use the existing Example Host discovery/navigation pattern.
- No database/provider transport is placed in reusable UI controls.

## Implementation checkpoint

The existing WorkItem image submission/storage contract is already authoritative and must not be duplicated. This slice adds the missing concrete WinForms host-context boundary over native WinForms controls.

Required inspection sources:
- `docs/architecture/v1-host-and-management.md`;
- `docs/architecture/foundations.md`;
- `docs/roadmap.md` 1.13;
- `docs/ui/examples.md`;
- existing WorkItem image contracts and Management facade;
- current Host.WinForms and Example Host composition/lifetime boundaries.

## Verification handoff

Example to run: `<to be established by the implementation>` — Hive.Example.WinForms
Tests to run: focused 1.13 host-context/image-input tests; broader `dotnet test tests/Hive.Tests/Hive.Tests.csproj` as required by the slice.

Do not close 1.13-A until the required automated verification and manual Example Host verification are actually reported.
