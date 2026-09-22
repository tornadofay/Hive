# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.12 — HiveSettingsForm & Configuration Pages**

Phase 0 — Foundations and Phase 1.1 through Phase 1.11 are complete and verified.

Phase 1.12 is the next authorized implementation slice.

## Objective

Establish the first-class Hive Settings surface as a thin WinForms shell over Hive.Management, with Provider and Persistence configuration pages.

## Scope

- Provider configuration and connection-test flow through the Management/configuration boundary.
- Persistence configuration through the Management boundary.
- SQL Server / LocalDB settings with credentials referenced through Secret Store.
- Non-destructive connectivity testing; database initialization/migration remains separate.
- Persisted settings reload after application restart without plaintext password exposure.
- Settings UI owns navigation/composition only; no direct SQL, provider transport, or migration implementation in WinForms.

## Verification gate

1. Management logic remains outside the WinForms shell.
2. Persistence configuration can be saved, reloaded, and validated through the public Management contract.
3. Connection-test success/failure is reported without schema side effects.
4. Database/schema status is distinct from connection success.
5. Credentials are not persisted in plaintext or exposed in diagnostics.
6. Developer manually verifies Provider and Persistence UI flows.
7. Focused automated coverage exists for configuration validation/persistence and security-sensitive behavior.

## Constraints

- Do not implement Phase 1.13 or later.
- Preserve existing Hive.Management and Hive.Persistence boundaries.
- Do not duplicate provider transport, secret storage, SQL connection/migration, or bootstrap logic in the Settings UI.
- Use the existing Example Host pattern for the public example required by the slice.

## Handoff

Example: add the authorized Settings example under the appropriate existing Example Host branch using `docs/ui/examples.md`.

Tests: add/run the focused configuration tests required by the slice, then run broader `Hive.Tests` before closure.

Read before coding: the 1.12 roadmap section, relevant `docs/architecture.md` configuration/persistence sections, current Management contracts, Secret Store, Persistence bootstrap/configuration code, Host UI conventions, and Example Host guidance.
