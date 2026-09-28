# Hive — Active Work

Status: IMPLEMENTATION IN PROGRESS

## Authorized revision

**Revision — Phase 1.16 UI — Provider Configuration, Discovery & Target Reconciliation**

This revision re-audits the immediately preceding Phase 1.16 UI slice and corrects only concrete same-slice defects.

## Recorded findings

1. The Advanced Execution Target editor presents the Automatic / Manual management selector, but the selected mode is ignored when creating a new ExecutionTarget; new targets are always created with the constructor default Manual.
2. The normal Provider Settings row labels providers without credentials (for example local providers whose catalog credential kind is None) as `Not configured`, which incorrectly implies missing configuration.
3. The shared CRUD Add action is hard-coded as `Add`; the normal Provider Settings requirement is the established `Add Provider` action wording.

## Scope

- fix new ExecutionTarget creation so the selected durable ExecutionTargetManagementMode is applied;
- correct normal Provider Settings credential status presentation for catalog-defined no-credential providers;
- extend the existing shared CRUD presentation with a bounded Add-action caption customization and use `Add Provider` on the normal Providers page;
- add focused regression tests for these behaviors;
- preserve the completed provider-first architecture, Management ownership, Advanced Configuration boundary, Automatic/Manual target semantics, and all existing behavior;
- no new roadmap capability, no Phase 1.17 work, no background discovery scheduling, and no agent target-selection work.

## Verification handoff

Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms
Tests to run: ProviderSettingsIntegrationTests.cs; relevant Hive UI test coverage; full Hive.Tests suite

Agent verification boundary: source/diff review only; no build, test run, or application launch is performed by the agent.
