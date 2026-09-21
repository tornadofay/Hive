# Hive — Active Work

Last updated: 2026-09-21

## Active slice

**0.5 — Test harness**

Phase 0.4 is complete and verified by the developer. Do not begin 0.6 or any later slice until 0.5 is complete and its verification has actually been performed.

## Objective

Establish the reusable test infrastructure required by later Hive slices:

- xUnit test conventions for the .NET 10 solution;
- deterministic fake provider infrastructure;
- fake clock infrastructure;
- test-database strategy for persistence tests;
- deterministic event-test conventions;
- clear separation between unit tests and real persistence/MAF/provider boundary tests.

## Implementation progress

0.5 implementation has not started yet.

The test project already contains slice-specific xUnit verification from Phases 0.2–0.4. This slice will consolidate reusable test infrastructure and conventions without duplicating or unnecessarily rewriting those tests.

## 0.4 completion record

Phase 0.4 was completed after the developer ran the full Hive.Tests suite against SQL Server.

- Build/test execution result: **PASS — developer test run completed successfully.**
- Persistence verification result: **PASS — 44 tests, 44 passed, 0 failed, 0 skipped, 3.3 seconds.**
- Database verification: **PASS — persistence integration tests executed against the developer SQL Server instance and Hive created the test databases and applied the bootstrap migration.**
- Verification result: **PASS for the 0.4 completion gate based on the developer-run suite.**
- Ready commit before starting 0.5: `6b2ed7703b659400779d9a46e943c7ad12ea9f8e`
- Next slice: **0.5 — Test harness**

## Out of scope for 0.5

- new persistence domain tables;
- provider production implementation;
- Agent/Hive behavior;
- MAF orchestration;
- WinForms UI;
- V1 image/document pipeline;
- CognitiveAgent/CognitiveHive;
- Example Host Shell beyond any test-harness developer tooling specifically required by this slice.

## 0.5 Verification

The implementation requires verification of:

1. reusable fake provider behavior is deterministic and does not call a real vendor;
2. fake clock behavior is deterministic and supports boundary-time tests;
3. persistence tests have a documented, repeatable database strategy without hidden environment-variable prerequisites;
4. event tests have deterministic serialization/upcasting conventions;
5. unit tests remain independent from real SQL Server except where the persistence boundary explicitly requires integration coverage;
6. the full suite continues to run through the normal Visual Studio solution test workflow.

Verification is currently pending. No 0.5 pass claim is recorded yet.

## Completion record

Complete this section after verification:

- Build result:
- Verification result:
- Commit:
- Next slice:

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