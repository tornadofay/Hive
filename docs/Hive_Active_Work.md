# Hive — Active Work

Status: VERIFICATION PENDING

## Authorized slice

**Phase 1.16 — UI — Provider Configuration, Discovery & Target Reconciliation**

Checkpoint: `5c7f21c433379cb771965e09b1e8e6d68bb59d99` (main, 2026-09-28)

## Objective

Implement the revised end-product Provider Settings architecture over the completed Phase 1.16 backend discovery capability.

Normal Settings is provider-centric:
- configured Providers are the only normal Provider Settings records shown;
- Add Provider selects a built-in provider catalog entry and collects the provider's required credential;
- Hive.Management creates/enables the durable Provider and default ProviderAccount, stores the credential through ISecretStore, and starts discovery/reconciliation outside the credential transaction;
- Refresh performs fresh discovery/reconciliation for active configured provider/account/endpoint contexts and refreshes safe provider operational summaries;
- normal setup never requires manual ProviderAccount or ExecutionTarget creation.

Advanced Configuration remains the generalized administrative surface for Providers, Accounts/Credentials, and ExecutionTargets.

## Automatic ExecutionTarget reconciliation

Successful fresh ProviderDiscoverySnapshot evidence at ProviderAccount + endpoint scope is reconciled into durable automatic ExecutionTargets.

Required semantics:
- no fake/placeholder persisted ExecutionTarget is created merely to discover models;
- automatic targets have explicit durable `ExecutionTargetManagementMode` (`Automatic` / `Manual`);
- reconciliation identity is stable across refreshes using ProviderAccount + endpoint + model/deployment identity;
- newly discovered models/deployments create automatic targets;
- rediscovered identities reuse existing automatic targets;
- missing identities from a successful fresh enumeration retire only the matching automatic targets without physical deletion;
- returning identities may reactivate existing automatic targets after normal lifecycle/dependency validation;
- unavailable/unhealthy models are not treated as missing catalog entries;
- failed, stale, unsupported, cancelled, malformed, rate-limited, or authentication-failing discovery preserves existing targets;
- Manual/administrator-managed targets are never overwritten by automatic reconciliation;
- returning a target to Automatic is an explicit Management operation.

## UI scope

Normal Providers page:
- shared Hive CRUD presentation;
- configured-provider rows only;
- safe masked credential/readiness/model-count/operational summary where available;
- toolbar actions: Add Provider, Refresh, Advanced;
- normal Edit replaces protected credential only; provider identity/transport changes remain Advanced;
- existing Provider lifecycle behavior remains authoritative.

Add Provider dialog:
- built-in provider catalog ComboBox;
- provider-specific required credential input;
- no manual account/target fields.

Advanced:
- generalized Provider / Accounts & Credentials / Execution Targets administration;
- provider-neutral entry point, optionally carrying a provider filter.

Settings navigation:
- normal top-level Settings leaves remain Providers, Agents, Persistence;
- Accounts/Credentials and ExecutionTargets are reached through Advanced rather than normal top-level Provider configuration.

## Ownership and exclusions

- WinForms remains presentation-only over Hive.Management.
- No direct provider HTTP/network calls, Secret Store access, SQL, migrations, or reconciliation logic in WinForms.
- Reuse the existing completed Phase 1.16 discovery/cache/security/cancellation/concurrency boundaries where they fit.
- Do not move capability-aware target selection into Provider Settings.
- Do not implement Agent `Auto` / fixed target selection here; that is owned by Phase 1.21.
- Do not add Workspace, MAF, Tool, authorization, business-write, cognition, membership, Swarm, or other future-phase behavior.
- Do not introduce periodic/background discovery scheduling in the Settings UI.
- Do not discard the existing advanced resource pages.

## Implementation state

Source/diff review complete at `5de6a0fd8c221bbe6b37420f2180c445a07f7d14` (main, 2026-09-28). The revised Provider Settings implementation is complete at the agent verification boundary; no build, test run, application launch, or external provider call was performed by the agent.

## Example / test handoff

Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms
Tests to run: ProviderSettingsIntegrationTests.cs; full Hive.Tests suite

## Verification gate

Required developer verification before closure:
- focused automated coverage for onboarding, credential replacement/secrecy, fresh refresh/reconciliation, automatic/manual ownership, lifecycle retirement/reactivation, concurrency/idempotency, and failure/cancellation preservation;
- full `Hive.Tests` suite;
- exact Example Host Provider Settings workflow manually verified;
- Treat Warnings as Errors / zero-warning confirmation using the standing Visual Studio configuration.

Agent verification boundary: source/diff review only unless explicitly authorized otherwise. No build/test/launch/provider call is claimed by the agent.
