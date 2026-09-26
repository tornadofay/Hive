# Hive.Example.WinForms — Agent Reference

## Example class

```csharp
internal sealed class ProviderExample : IHiveExample
{
    public string Category => "Providers";
    public string Subcategory => "Provider Platform";

    public int Order => 10;
    public string Title => "Provider / ProviderAccount / ExecutionTarget";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new ProviderExampleView(services);
    }
}
```

Requirements:
- concrete `IHiveExample`;
- parameterless constructor;
- return a `UserControl`.

No central registration. The host builds:

```text
Category
└─ Subcategory
   └─ Additional navigation groups (optional)
      └─ Example Title
```

## Tree placement

`Category` and `Subcategory` define the base path. `AdditionalNavigationPath` adds any deeper grouping levels; `Title` is always the selectable leaf.

Current branches:
- `UI → Foundation`
- `Agents → Base Agent`
- `Providers → Provider Platform`
- `Providers → Target Selection`
- `Providers → Provider Transport`
- `Management → Facade`
- `Workspace → WorkItem Operations`
- `Settings → Configuration`
- `Host → WinForms Integration`
- `Persistence → Events → Event Persistence`
- `Persistence → Events → Outbox Poller`

For a new Example:
1. Reuse the existing Category/Subcategory that matches the capability.
2. Add `AdditionalNavigationPath` when a capability needs its own independently expandable group.
3. Use exact existing spelling/casing for existing groups.
4. Create additional groups only when they improve organization.
5. Do not edit `HiveExampleHostForm`; it builds every path automatically.

Examples:
- UI control/theme/dialog/CRUD example → `UI / Foundation`
- Base Agent / AgentFactory example → `Agents / Base Agent`
- Base Agent work protocols example → `Agents / Base Agent`
- First real MAF-backed Base Agent execution example → `Agents / Base Agent`
- Configured-host Agent execution example → `Agents / Base Agent`; uses the Example Host's selected persisted `AgentDefinition` and `IHiveManagementFacade.ExecuteConfiguredAgentAsync`, not a private database or synthetic execution target.
- Provider/ProviderAccount/ExecutionTarget example → `Providers / Provider Platform`
- Capability-aware execution target selection example → `Providers / Target Selection`
- OpenAI-compatible provider transport example → `Providers / Provider Transport`
- Hive.Management CRUD facade example → `Management / Facade`
- V1 Workspace / WorkItem operations example → `Workspace / WorkItem Operations`
- Durable event log/snapshot/outbox example → `Persistence / Events / Event Persistence`
- Image input and WinForms host-context discovery example → `Host / WinForms Integration`

The Phase 1.13 WinForms host-context example uses a deterministic fixture Form and the checked-in image fixture so it does not require a real business application or real provider account. Discovery returns read-only metadata snapshots; it never grants control-action authority.

The verified Phase 1.14 Example Host scenario demonstrates the public Hive-owned host contracts and contract-first semantic integration model. Its deterministic fixture uses native WinForms controls plus parent/child data surfaces with a hidden primary-key ID, and demonstrates semantic field metadata, stable row identity, generated/computed fields, bounded child-row interaction, direct editor/grid patterns, dependent lookup behavior, host validation/business-operation boundaries, UI capability versus Hive authorization, and API/UI capability composition without exposing raw control handles, SQL, private host classes, or private library APIs.

The reopened Phase 1.14 revision extends this same scenario to demonstrate the preferred low-code WinForms path: Hive-owned base forms/controls provide common integration behavior automatically, while explicit semantic overrides are used where application meaning cannot be safely inferred. The example should also retain coverage of the adapter/semantic-provider compatibility path for ordinary/native/custom controls.

For the planned V1 business-write/review examples across Phases 1.21–1.23, the Example Host should demonstrate the public business-operation boundary with a deterministic fake host/application. The scenario should show a structured proposal, PendingApproval when required, a durable BusinessOperationReceipt/attempt record with parent/child host identities, a simulated unknown outcome with reconciliation, and a first-class Review queue/list that can locate the written records, open/navigate to the associated host record through the bounded host-review capability, and record a correct or incorrect result with evidence. The scenario should demonstrate the same stable logical operation identity across retry/reconciliation and the policy distinction between required human review, automated/hybrid verification, and an operation class for which human review is not required. The example must not use a real business database or real credentials.

Planned V1 interaction and operations examples should also cover:
- Provider/model capability discovery and operational metadata → `Providers / Target Selection / Capability Discovery`;
- direct LLM and Agent Workspace interaction, including application-wide and form-associated specialist Agents → `Workspace / Agent Interaction`;
- multiple independent Agents assigned concurrent WorkItems without Hive/Swarm membership → `Workspace / Multi-Agent Work Assignment`;
- governed Tool, policy, permission, and human-intervention behavior → `Management / Governance`;
- authoritative resource inventory and runtime/execution diagnostics → `Operations / Resource Inventory`;
- durable Base-Agent work state and bounded vector retrieval → `Persistence / Agent Work State`.

The host-level Hive Settings entry is introduced through the Overview / Getting Started configuration example. The example explains the configuration model and opens the real Settings window; it is not a fake configuration-inspection surface.

Future examples can create independent branches without changing the host:

```text
Persistence
└─ Events
   ├─ Event Persistence
   │  └─ Event Log + Snapshot + Outbox
   ├─ Event Log
   │  ├─ Append / Read
   │  └─ Concurrency
   ├─ Snapshots
   │  └─ ...
   └─ Outbox
      └─ ...
```

`Order` controls deterministic example ordering. `Title` is the selectable leaf text.

## Example classification

Examples are classified by the dependency model they prove:

| Classification | Examples |
|---|---|
| **Configured-host** | `Agents / Base Agent / Configured Agent Execution` — consumes the persisted Provider → ProviderAccount → ExecutionTarget → AgentDefinition graph from the host service graph and must not create a competing persistence/configuration path. |
| **Isolated contract** | `Workspace / WorkItem Operations`; `Management / Facade`; `Agents / Base Agent / First Real Agent Execution`; `Providers / Security / DPAPI Secret Store`; `Providers / Provider Platform`; `Persistence / Events / Event Persistence`; `Persistence / Events / Outbox Poller` — these intentionally use deterministic/example resources or `HiveDatabaseOptions.LocalDevelopment()` where that local database is intrinsic to the contract being demonstrated. |
| **Host configuration surface** | `Overview / Getting Started / Example Configuration` — opens the real global Hive Settings surface; it is infrastructure guidance, not an isolated database example and not a competing configuration model. |
| **Host integration** | `Host / WinForms Integration / Image Input & WinForms Host Context` — concrete bounded WinForms host-context discovery; `Host / WinForms Integration / Dual Business-App Integration Contract` — contract-first semantic host integration with deterministic business-app fixtures. |

The current `HiveDatabaseOptions.LocalDevelopment()` uses were reviewed during Phase 1.12-G and intentionally remain isolated. They are not evidence that configured-host examples may bypass the host service graph.
## Settings as package configuration

The global Hive Settings surface is host infrastructure, not a replacement for individual Examples.

The Example Host exposes the real Settings center through:

**Overview / Getting Started / Example Configuration**

That leaf explains the configuration model and opens the host-level Settings window. It does not create a second configuration model or replace Settings with an Example.

Current Settings resource hierarchy:

```
Providers
├── Provider Configuration
├── Accounts / Credentials
└── Execution Targets
Agents
Persistence
```

Provider Configuration, Accounts / Credentials, and Execution Targets are separate CRUD Settings pages because their Management contracts are separate resources. Accounts / Credentials are not provider login screens; they identify durable credential/resource records used by execution targets. Execution Targets own the concrete endpoint/model/deployment/capability configuration used for execution. Agents are also a separate CRUD page that references an ExecutionTarget. Persistence is different: it is a single global configuration editor rather than a CRUD collection.

Later durable configuration such as Runtime / Execution Defaults, Cognition, Knowledge, Skills, and Phase 5 semantic Memory extends the same Settings center only after its authoritative contract exists. V1 Tools, Policy / Permissions, and Base-Agent work state remain under their V1 Management and persistence boundaries.

The Example Host must consume the same configured Hive state a real host would consume. It must not construct a competing Hive management/persistence graph or hard-code `HiveDatabaseOptions.LocalDevelopment()` when saved configuration exists.

Configured-host Examples must consume the host's current Hive service graph; they must not construct a competing `HiveManagementFacade`/persistence graph or hard-code `HiveDatabaseOptions.LocalDevelopment()` when saved configuration exists.

## Shared services

```csharp
var theme = services.GetThemeManager();
var output = services.GetExampleOutput();
```

Services:
- `IHiveThemeManager`
- `IHiveExampleOutput`

## Example view

```csharp
var surface = new HiveExampleTestSurface
{
    RunButtonText = "Run example"
};

surface.SetInformation("Description", "Expected result");
surface.CodeSnippet = """
// public API
""";
surface.ConfigureRun(RunScenarioAsync, output, FindForm());
```

Run the real public API and honor the cancellation token.

## Output

```csharp
output.Write("Provider", $"Id: {provider.Id}");
output.Append($"{Environment.NewLine}Version: {provider.Version}");
```

`Write` replaces output; `Append` adds text.

Never output secrets or credentials.

## Theme

The host themes the selected view.

For additional dynamic controls:
```csharp
theme.Apply(childControl);
```

## Public API

Do not call internal production helpers, mutate persistence tables directly, bypass Management for management operations, or use Hive.Tests types as application shortcuts.

## Handoff

```text
Example to run: <Category / Subcategory / additional path / Example title> — Hive.Example.WinForms
Tests to run: <focused test class/file>; broader-suite requirement if applicable
```

Keep the exact Example path in `docs/Hive_Active_Work.md` while verification is pending.

- V1 Input Preparation & Routing example → `Workspace / WorkItem Operations / Input Preparation & Routing`
