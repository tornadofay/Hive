# Hive.Example.WinForms — API Quick Reference

Examples demonstrate public Hive APIs. They are not a second test framework.

## Add an Example

Normal pattern:

```csharp
internal sealed class ProviderResourceExample : IHiveExample
{
    public string Category => "Providers";
    public string Subcategory => "Provider Platform";
    public int Order => 10;
    public string Title => "Provider / ProviderAccount / ExecutionTarget";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new ProviderResourceExampleView(services);
    }
}
```

No central registration is required.

Discovery uses the Example Host assembly and requires:
- concrete type;
- IHiveExample;
- parameterless constructor.

Sorted by Order, Category, Subcategory, Title.

## View

Put the scenario in a focused UserControl.

Typical choice:

```text
Example view
  └─ HiveExampleTestSurface
       └─ shared Example output
```

Use native WinForms controls when sufficient.

## Shared services

Use:

```csharp
var themeManager = services.GetThemeManager();
var output = services.GetExampleOutput();
```

Available shared services:
- IHiveThemeManager
- IHiveExampleOutput

Do not access HiveExampleHostForm internals.

## Example test surface

```csharp
var surface = new HiveExampleTestSurface
{
    RunButtonText = "Run provider CRUD"
};

surface.SetInformation(
    "Creates and reads the resource through the public API.",
    "The expected resource state is shown in Output.");

surface.CodeSnippet = """
// Public API reproduction
""";

surface.ConfigureRun(
    RunScenarioAsync,
    output,
    FindForm());
```

The run action must call the real API and honor its cancellation token.

On failure, let the shared surface handle the exception unless the scenario has a specific reason to map it differently.

## Output

```csharp
output.Write("Provider", $"Id: {provider.Id}");
output.Append($"{Environment.NewLine}Version: {provider.Version}");
```

`Write` replaces current output. `Append` adds text.

Never output secrets, credentials, or access tokens.

## Theme

The Host themes the created view before showing it.

For a dynamically created child subtree:

```csharp
services.GetThemeManager().Apply(child);
```

Do not create another theme manager.

## Public API rule

Examples must use the same public contracts intended for real consumers.

Do not:
- call internal production helpers;
- mutate persistence tables directly;
- bypass Management for management behavior;
- use Hive.Tests types as application shortcuts.

## Required Example/Test handoff

For every new meaningful externally usable capability:

```text
Example to run: <Category / Subcategory / Example title> — Hive.Example.WinForms
Tests to run: <focused test class/file>; broader-suite requirement if applicable
```

Keep the exact path in docs/Hive_Active_Work.md while verification is pending.

## Do not edit the Host for a normal Example

Adding an Example normally means adding:
- the IHiveExample class;
- the scenario UserControl.

Do not modify HiveExampleHostForm unless the discovery, navigation, shared services, or active-view composition contract itself changes.
