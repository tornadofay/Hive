# Hive — Roadmap (ordered, slice-level implementation path)

This is the granular companion to `docs/architecture.md`. Each slice is sized to be one GPT session (run-bounded execution).

When a slice starts, copy its Objective / Files / Verify into `docs/Hive_Active_Work.md`.

Status belongs only in `docs/Hive_Current_Status.md`.

Numbering is `Phase.Slice`; it is an ordinal implementation sequence, not a version number.

Phases are deliberately ordered so the platform foundations exist before higher-level cognition, management, and Hive governance depend on them.

---

# Phase 0 — Foundations

## 0.1 — Solution and project scaffolding

**Objective:** create the solution and projects:

```text
Hive.Core
Hive.Agents
Hive.Persistence
Hive.Coordination
Hive.Tools
Hive.Providers.OpenAICompatible
Hive.Management
Hive.Host.WinForms
Hive.Example.WinForms
Hive.Tests
```

**Reference rule:** Host.WinForms and Example.WinForms use Hive.Management for management/persistence operations and do not bypass the facade.

**Verify:** solution builds; project references match architecture.

## 0.2 — Common infrastructure

IDs, immutable value objects, typed errors/results, `IClock`, JSON configuration, event envelope, correlation/causation IDs.

**Verify:** unit tests for identity/equality, fake clock, JSON round-trip, typed errors.

## 0.3 — Identity and Resource foundation

Implement Deployment, Tenant, Principal, User, Session, Workspace, Agent, Runtime, Execution, Resource, ResourceScope, ownership keys, provenance, and lifecycle/version metadata.

**Verify:** scope/ownership matrix, missing-identity fail-closed tests, immutable identity snapshot tests.

## 0.4 — Persistence bootstrap

Hive-owned SQL Server database, LocalDB development setup, DbUp migrations, schema-version tracking, indexes.

**Verify:** clean install, repeat migration, failed migration, unknown future schema version.

## 0.5 — Test harness and example host foundation

Scaffold xUnit, shared fixtures, fake provider infrastructure, fake clock, test database strategy, deterministic event-test conventions, and the initial `Hive.Example.WinForms` shell.

**Verify:** baseline tests pass; the example host builds; automated tests make no vendor/network calls.

---

# Phase 1 — Provider Platform and Execution

## 1.1 — Provider / ProviderAccount / ExecutionTarget

Concrete resource types and persistence.

**Verify:** create/read/update, ownership/scope, indexes, duplicate identity handling.

## 1.2 — Secret storage and redaction

`ISecretStore`, DPAPI-backed implementation, secure replacement/deletion, diagnostic redaction.

**Verify:** plaintext never persists, diagnostics never contain secrets, replacement invalidates previous credential.

## 1.3 — OpenAI-compatible provider adapter

One shared adapter for compatible hosted/local endpoints.

Add the provider example page and complete public-API snippet to `Hive.Example.WinForms`.

**Verify:** fake HTTP provider covers success, malformed response, timeout, cancellation, authentication failure, rate limit, transport failure, structured-output failure; the example uses the same public provider contract.

A manual smoke test may be performed against a real provider outside automated tests.

## 1.4 — Capability evidence

Three-state capability records:

```text
Supported
Unsupported
Unknown
```

Evidence/provenance is stored separately from the capability decision.

**Verify:** unknown is never promoted to supported for a required capability.

## 1.5 — Rich execution requirements

Implement Required, Preferred, Optional, and Forbidden.

**Verify:** deterministic filtering and diagnostics.

## 1.6 — Execution selection policy

Implement Auto, Preferred, Fixed and FreeOnly, FreePreferred, NoRestriction.

Separate technical capability from cost and authorization.

**Verify:** policy matrix and explanation tests.

## 1.7 — Execution planner

`IExecutionPlanner` returns a provider-neutral `ExecutionPlan`.

**Verify:** candidate ranking, rejection diagnostics, no qualifying target, fixed-target failure, unknown-cost behavior.

## 1.8 — Agent Definition / Runtime / Execution

Implement the three identity levels:

```text
Agent Definition
Runtime Instance
Execution
```

**Verify:** multiple runtime instances from one definition do not share mutable state.

## 1.9 — Immutable execution snapshot

Snapshot provider settings, agent configuration, capability assignments, runtime overrides, identity, budget, and applicable policy.

**Verify:** live configuration edits never change an already-running execution.

## 1.10 — Execution lifecycle and budgets

Cancellation, timeout, budget caps, terminal state protection.

**Verify:** cancellation/timeout races, late provider completion, budget exhaustion, duplicate terminal transitions.

---

# Phase 2 — Persistent Cognition and First-Class Resources

## 2.1 — Cognitive Kernel

Stable runtime substrate for cognitive identity, lifecycle, state revision, persistence, event activation, recovery, and concurrency ownership.

**Verify:** state revision ordering, recovery, concurrent mutation protection.

## 2.2 — Cognitive Strategy

Replaceable strategy contract.

A strategy may choose deterministic action, no model call, tool action, or model reasoning.

**Verify:** deterministic path does not invoke provider; strategy replacement does not change kernel contracts.

## 2.3 — Reasoning Requirement

Provider-neutral reasoning requirements separated from execution target selection.

**Verify:** the same cognitive requirement can be planned onto different compatible targets.

## 2.4 — Persistent goals, beliefs, intentions, plans

Implement durable cognitive state.

**Verify:** restart/recovery preserves state; invalid transitions rejected; stale revisions rejected.

## 2.5 — Experience and cognitive event history

Capture bounded evidence and outcomes.

**Verify:** correlation/causation, bounded payloads, replay of supported transitions.

## 2.6 — Memory resource family

Implement working, episodic, semantic, procedural, and extensible memory-family contracts.

**Verify:** runtime-private memory isolation, shared-memory authorization, scope enforcement.

## 2.7 — Knowledge / Wiki

Knowledge resource, Wiki source, provenance, versions, relationships, retrieval contract.

**Verify:** source/version lifecycle, permission checks, bounded retrieval.

## 2.8 — Skills

Versioned reusable skills, dependencies, constraints, capability assignments.

Executable handlers remain runtime registrations.

**Verify:** versioning, dependency validation, disabled-skill enforcement.

## 2.9 — Learning Candidates and governance

Candidate generation, evidence, confidence, provenance, review state, promotion/rejection.

**Verify:** candidates cannot directly mutate authoritative resources.

## 2.10 — Capability assignments and runtime overrides

Implement profile defaults plus runtime Inherit / Enabled / Disabled.

**Verify:** effective state is correct and captured in execution snapshots.

## 2.11 — Generic Resource Inventory

Generic inventory API plus typed projections.

**Verify:** future/unknown resource types remain visible without new Agent properties.

## 2.12 — Death / postmortem / reincarnation

Execution/runtime/agent/cognitive lifecycle semantics, durable evidence, candidate deductions, governed reincarnation.

**Verify:** dead state is not deleted; reincarnation references prior durable history; concurrent postmortem runs remain deterministic.

---

# Phase 3 — Host Integration

## 3.1 — Generic Host Context contract

Typed bounded host observations and context snapshots without host-domain coupling.

**Verify:** size limits, provenance, redaction, immutable snapshots.

## 3.2 — Host identity propagation

Identity context through execution, tools, events, memory, knowledge, learning, policy, and tracing.

**Verify:** identity is preserved and never inferred from model text.

## 3.3 — WinForms UI Context

Support Forms, UserControls, control trees, bounded properties, and bindings.

**Verify:** bounded traversal, cancellation, redaction, no executable reflection path.

## 3.4 — Native data-source adapters

Support common native .NET representations directly:

- BindingSource;
- DataTable / DataView / DataSet;
- arrays;
- IList / IReadOnlyList / IBindingList;
- dictionaries/key-value collections;
- POCOs/records;
- bounded IEnumerable sources;
- bound controls;
- explicit projections.

**Verify:** native sources are preserved where practical; representations do not require conversion to DataTable.

## 3.5 — Object discovery

Bounded read-oriented discovery of host-owned objects.

**Verify:** depth/item limits, cycle handling, unsupported type handling, no authority escalation.

## 3.6 — Control Adapters

Explicit adapters for controls/data sources where generic discovery is insufficient.

**Verify:** adapter registration, isolation, malformed host objects, lifecycle cleanup.

## 3.7 — Host-side tool boundary

Turn explicit host capabilities into governed tools.

**Verify:** discoverability does not imply tool authorization; unauthorized tools remain unavailable.

## 3.8 — Document/image extraction example workload

Implement parsing, vision routing, structured extraction, validation as a normal capability built from the platform.

**Verify:** this workload passes entirely through the same provider/planner/context/tool/policy infrastructure as other workloads.

---

# Phase 4 — Management, Intervention and Portability

## 4.1 — Hive.Management facade

Authoritative CRUD/query/action services over Core/Persistence/Agents/Cognition.

**Verify:** management operations use the same underlying state and policy as runtime APIs.

## 4.2 — WinForms configuration shell

`HiveSettingsForm` + `HiveConfigurationContext`.

**Verify:** navigation is declarative and the shell contains no feature-specific persistence logic.

## 4.3 — Providers / Models / Execution Targets page

**Verify:** add/edit/remove provider/account/target, capability evidence, test connection, redaction.

## 4.4 — Agents page

**Verify:** agent definitions, resource assignments, runtime defaults, selection policy.

## 4.5 — Cognition page

**Verify:** inspect cognitive state, lifecycle, goals, plans, revision, protected intervention entry points.

## 4.6 — Learning Review page

**Verify:** inspect candidate provenance/evidence/confidence, approve/reject, stale/concurrent review protection.

## 4.7 — Knowledge / Wiki page

**Verify:** CRUD, versioning, provenance, permissions, bounded retrieval.

## 4.8 — Skills page

**Verify:** versioning, dependencies, assignments, enable/disable policy.

## 4.9 — Storage page

**Verify:** LocalDB configuration, migration status, connection test, safe diagnostics.

## 4.10 — Runtime Diagnostics page

Show runtime identity, execution state, cognitive revision, target, budget, correlation IDs, intervention state, and safe provider operational data.

**Verify:** secrets/sensitive payloads remain redacted.

## 4.11 — Generic Resource Inventory UI

**Verify:** known types get specialized views; unknown types still appear.

## 4.12 — General Human Intervention

Implement Inspect / Approve / Reject / Pause / Resume / Cancel / Retire / Shutdown / Redirect / Defer / RequestInformation.

**Verify:** stale state/version, concurrency, authorization, terminal request behavior.

## 4.13 — Configuration portability

Export/import authoritative configuration:

- Providers;
- Models;
- Execution Targets;
- Agents;
- Hives;
- Skills;
- Knowledge / Wiki;
- Memory configuration;
- Learning configuration;
- Tools;
- Permissions;
- policies;
- supported runtime defaults.

**Verify:** version compatibility, conflicts, round-trip, credentials omitted by default, protected credential-bearing export.

---

# Cross-cutting Example + Test Rule

The example application and production tests are developed **alongside each feature**, not postponed into a separate phase.

Each implementation slice decides which layers are necessary:

- unit tests;
- contract/integration tests;
- targeted system/end-to-end tests;
- concurrency/recovery tests;
- example page/scenario;
- complete public-API code snippet;
- WinForms smoke/automation coverage where required.

## Example host foundation

Create `Hive.Example.WinForms` during the early implementation work once the public Core/Management boundary exists.

The example host is a thin consumer of the same public APIs used by real hosts.

Examples are added beside the features they demonstrate:

- execution and planning;
- persistent cognition;
- Skills;
- Knowledge / Wiki;
- Memory;
- Learning;
- human intervention;
- configuration portability;
- host UI Context / Control Adapters;
- diagnostics;
- runtime isolation and recovery;
- document/image extraction as one workload.

## Production test rule

Tests are added with the feature that introduces the behavior.

Use the lowest test layer that can actually prove an invariant:

```text
unit
  ↓
contract/integration
  ↓
targeted system/end-to-end
  ↓
UI smoke / automation where required
```

Do not create a later testing phase to catch up on coverage.

---

# Phase 5 — Hive Membership & Coordination

## 5.1 — Hive Definition

## 5.2 — Membership and roles

## 5.3 — Configurable agent composition

## 5.4 — Message/DTO communication

## 5.5 — Shared claims with provenance

## 5.6 — Supervisor controls

All coordination uses MAF workflow/orchestration primitives where applicable.

---

# Phase 6 — Hive Governance Patterns

## 6.1 — Manager-led strategy

## 6.2 — Democratic/voting strategy

## 6.3 — Adversarial/critique strategy

## 6.4 — Conflict resolution

## 6.5 — Governance strategy selection and policy UI

---

# Phase 7 — Cognitive Safety, Scale and Extensibility

## 7.1 — Goal drift / embedding checks

## 7.2 — Credit assignment

## 7.3 — Belief revision

## 7.4 — Cognitive safety policies

## 7.5 — Authentication boundary

## 7.6 — Distributed execution decision point

Re-evaluate distributed infrastructure only from measured requirements.

## 7.7 — MCP/tool extensibility

## 7.8 — Additional host surfaces

Web/WPF or other hosts use the same Management/Core contracts.

---

# Phase 8 — Observability, Operations and Replay

## 8.1 — Full metrics taxonomy

## 8.2 — Dashboards and operational views

## 8.3 — CI/CD

## 8.4 — Event-log replay regression

## 8.5 — Long-running resilience tests

## 8.6 — Production diagnostics and support tooling

---

# Phase 6 — Hive Membership & Coordination

## 6.1 — Hive Definition

## 6.2 — Membership and roles

## 6.3 — Configurable agent composition

## 6.4 — Message/DTO communication

## 6.5 — Shared claims with provenance

## 6.6 — Supervisor controls

All coordination uses MAF workflow/orchestration primitives where applicable.

---

# Phase 7 — Hive Governance Patterns

## 7.1 — Manager-led strategy

## 7.2 — Democratic/voting strategy

## 7.3 — Adversarial/critique strategy

## 7.4 — Conflict resolution

## 7.5 — Governance strategy selection and policy UI

---

# Phase 8 — Cognitive Safety, Scale and Extensibility

## 8.1 — Goal drift / embedding checks

## 8.2 — Credit assignment

## 8.3 — Belief revision

## 8.4 — Cognitive safety policies

## 8.5 — Authentication boundary

## 8.6 — Distributed execution decision point

Re-evaluate distributed infrastructure only from measured requirements.

## 8.7 — MCP/tool extensibility

## 8.8 — Additional host surfaces

Web/WPF or other hosts use the same Management/Core contracts.

---

# Phase 9 — Observability, Operations and Replay

## 9.1 — Full metrics taxonomy

## 9.2 — Dashboards and operational views

## 9.3 — CI/CD

## 9.4 — Event-log replay regression

## 9.5 — Long-running resilience tests

## 9.6 — Production diagnostics and support tooling

---

## Slice rule

Each active slice must be complete for its intended scope, production-safe against known edge cases, and compatible with documented future phases.

Do not build future-phase implementation early merely because the architecture already mentions it.
