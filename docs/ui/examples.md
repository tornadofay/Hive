# Hive.Example.WinForms — API

Examples use public Hive APIs and are discovered automatically.

## Add an Example

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

No central registration is required.

Navigation is `Category → Subcategory → Example`.

## Shared services

```csharp
var theme = services.GetThemeManager();
var output = services.GetExampleOutput();
```

Available services:
- `IHiveThemeManager`
- `IHiveExampleOutput`

## Example view

For an interactive scenario:

```csharp
var surface = new HiveExampleTestSurface
{
    RunButtonText = "Run example"
};

surface.SetInformation(
    "What this demonstrates.",
    "What should happen.");

surface.CodeSnippet = """
// Public API reproduction
""";

surface.ConfigureRun(
    RunScenarioAsync,
    output,
    FindForm());
```

The run action must call the real public API and use its cancellation token.

## Output

```csharp
output.Write("Provider", $"Id: {provider.Id}");
output.Append($"{Environment.NewLine}Version: {provider.Version}");
```

`Write` replaces output. `Append` adds text.

Never output secrets or credentials.

## Theme

The Host themes the selected view. For additional dynamic controls:

```csharp
theme.Apply(childControl);
```

Do not create another theme manager.

## Public API rule

Do not:
- call internal production helpers;
- mutate persistence tables directly;
- bypass Management for management operations;
- use Hive.Tests types as application shortcuts.

## Required handoff

For every new meaningful externally usable capability:

```text
Example to run: <Category / Subcategory / Example title> — Hive.Example.WinForms
Tests to run: <focused test class/file>; broader-suite requirement if applicable
```

Keep the exact path in `docs/Hive_Active_Work.md` while verification is pending.

## Host registration

Do not edit `HiveExampleHostForm` for a normal Example. Add the `IHiveExample` class and its view.
