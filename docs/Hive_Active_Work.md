# Hive — Active Work

Status: IN PROGRESS

Slice: Maintenance — Review Medium-Finding Corrections

Opened: 2026-09-27

Checkpoint: 7d84a68f7101df715d429b5d55c6770aff389e68

## Authorized scope

Correct the four concrete medium-severity findings from the repository review immediately preceding this slice:

1. Outbox lease lifetime — prevent a long-running delivery from silently losing its lease while the handler is still executing; preserve at-least-once/idempotent delivery semantics.
2. WinForms host interaction freshness — bind consequential interaction requests to the host capture they came from so stale captured authorization cannot mutate a replaced/recreated current control or semantic surface.
3. UI exception diagnostics — prevent raw exception details from exposing credential/connection/secret material through the Output panel or user-visible technical details.
4. ProviderAccount credential references — prevent ProviderAccount records from accepting unresolved Secret references and prevent referenced secrets from being hard-deleted while still in use.

## Boundaries

- Backend/Persistence and Host/UI corrective work only.
- Preserve existing V1 architecture, Management ownership, host-neutral contracts, and at-least-once outbox semantics.
- No new roadmap capability, provider transport, MAF/orchestration work, business write implementation, or cognitive work.
- Public contract changes are allowed only where directly required to close the identified existing correctness/security boundary; do not widen them beyond that purpose.
- Add focused regression coverage for each corrected failure mode.
- Work directly on main; no feature branch or PR.

## Implementation targets

- Hive.Persistence outbox claim/renew/complete boundary and poller lifecycle.
- Hive.Core / Hive.Management / Hive.Host.WinForms interaction freshness contract and enforcement.
- Hive.Host.WinForms.UI exception-diagnostics sanitization boundary.
- Hive.Persistence ProviderAccount/Secret reference integrity.
- Relevant tests and verification documentation.

## Verification

Status: NOT YET RUN

Required developer verification before closure:

Example to run: existing affected Example Host scenarios for Outbox Poller, WinForms Host Integration / Dual Business-App Integration, and Provider Accounts / Security as applicable.

Tests to run: focused regression tests added by this slice; full Hive.Tests suite.

Do not close the slice until developer verification results are recorded.