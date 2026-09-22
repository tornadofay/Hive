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
- Provider/ProviderAccount/ExecutionTarget example → `Providers / Provider Platform`
- Capability-aware execution target selection example → `Providers / Target Selection`
- OpenAI-compatible provider transport example → `Providers / Provider Transport`
- Durable event log/snapshot/outbox example → `Persistence / Events / Event Persistence`

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
