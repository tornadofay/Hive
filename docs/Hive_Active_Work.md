# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.4 — Capability-aware Execution Target Selection**

Phase 0 — Foundations, Phase 1.1 — Provider / ProviderAccount / ExecutionTarget, Phase 1.2 — Secret Store, and Phase 1.3 — OpenAI-compatible Provider Adapter are complete and verified.

Do not introduce 1.5 or later Phase 1 slices until 1.4 is complete.

## Objective

Establish capability-aware selection over the existing ExecutionTarget contract:

- filter execution targets against explicit capability requirements;
- honor Supported / Unsupported / Unknown capability evidence;
- distinguish Required, Preferred, Optional, and Forbidden requirements;
- support Auto, Preferred, and Fixed selection modes;
- keep capability matching independent of provider identity;
- keep cost policy separate from capability requirements;
- return explainable selection diagnostics for accepted, rejected, and unavailable targets;
- fail deterministically when no target qualifies or a fixed target cannot satisfy requirements.

This slice consumes the established Provider / ProviderAccount / ExecutionTarget / capability contracts and the completed OpenAI-compatible transport boundary. It does not add Agent execution, MAF execution integration, Management settings UI, or planner behavior beyond the capability-aware target-selection boundary required here.

## Phase 1.3 completion

Phase 1.3 — OpenAI-compatible Provider Adapter is complete and verified.

Developer verification:
- Hive.Example.WinForms `Providers / Provider Transport / OpenAI-compatible Provider Adapter` completed successfully against the local fake HTTP endpoint, including normal and structured-output responses.
- Full `Hive.Tests` execution: **80 tests passed, 0 failed, 0 skipped in 1.6 seconds**.
- The 1.3 completion gate is satisfied.

## Architecture / dependency boundary

The selection boundary remains provider-neutral:

```text
Provider / ProviderAccount / ExecutionTarget
        ↓
Capability evidence + Requirements + Selection mode + Cost policy
        ↓
Capability-aware target selection
        ↓
Selected ExecutionTarget + explainable diagnostics
        ↓
future Agent / MAF execution
```

Selection must not embed vendor-specific transport behavior. The existing OpenAI-compatible adapter remains the transport implementation for compatible targets; capability-aware selection decides whether a configured target qualifies.

## Verification

Required for completion of 1.4:

1. supported capability satisfies a Required requirement;
2. Unsupported capability is excluded from a Required requirement;
3. Unknown capability is excluded from a Required requirement;
4. Preferred / Optional / Forbidden requirements behave according to the contract;
5. Auto / Preferred / Fixed selection modes are deterministic and distinct;
6. no qualifying target returns the required typed failure/diagnostics;
7. a Fixed target that cannot satisfy requirements is rejected;
8. selection diagnostics explain qualifying and rejected targets without leaking secrets;
9. selection remains independent of provider transport implementation;
10. focused automated test coverage exists for normal, invalid, and boundary cases;
11. broader `Hive.Tests` execution;
12. public Example Host verification if the externally usable selection surface requires an example under the slice gate.

No verification claim is recorded until it has actually been performed.

## Constraints

- No 1.5 or later AgentFactory/Agent implementation.
- No MAF execution integration.
- No Management settings/configuration UI.
- No new provider-specific adapters.
- No ProviderAccount credential-persistence changes.
- No changes to the SQL Server/DPAPI persistence boundary.
- Preserve the existing Provider / ProviderAccount / ExecutionTarget and capability contracts unless the active requirement proves a contract gap.
- Do not introduce cost selection as a substitute for capability matching; cost remains a separate policy input.
- Do not add a general-purpose workflow/orchestration engine.

## Implementation checkpoint

The 1.4 implementation is present in the repository at this checkpoint:

- `Hive.Core` exposes capability requirements, selection modes, separate cost-policy input, explainable selection diagnostics, and `ExecutionTargetSelector`.
- Required capabilities accept only `Supported`; Forbidden capabilities accept only explicitly `Unsupported`; missing capability evidence is `Unknown`.
- Preferred and Optional requirements influence deterministic ranking without becoming hard filters.
- Auto selects the highest-scoring qualifying target; Preferred honors a qualifying preferred target before fallback; Fixed never falls back.
- Inactive execution targets are rejected during selection.
- Selection diagnostics expose only target identity/key/name, score, status, and policy reasons; they do not expose endpoint or credential material.
- `Hive.Tests/ExecutionTargetSelectionTests.cs` contains focused coverage for capability states, requirement kinds, selection modes, lifecycle rejection, deterministic selection, failures, diagnostics, and cost-policy separation.
- `Hive.Example.WinForms` contains `Providers / Target Selection / Capability-aware Execution Target Selection`.
- `docs/examples/Phase14_Capability_Aware_Execution_Target_Selection.md` documents the public API and selection rules.

Developer verification is pending. Agent-run builds/tests/manual verification remain unauthorized.

## Verification handoff

Example to run: Providers / Target Selection / Capability-aware Execution Target Selection — Hive.Example.WinForms (net10.0-windows).

Tests to run: tests/Hive.Tests/ExecutionTargetSelectionTests.cs; broader Hive.Tests execution is required by the 1.4 completion gate.
