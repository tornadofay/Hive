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

No central registration. Navigation: `Category → Subcategory → Example`.

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

Host themes the selected view. For additional dynamic controls:
```csharp
theme.Apply(childControl);
```

## Public API

Do not call internal production helpers, mutate persistence tables directly, bypass Management for management operations, or use Hive.Tests types as application shortcuts.

## Handoff

```text
Example to run: <Category / Subcategory / Example title> — Hive.Example.WinForms
Tests to run: <focused test class/file>; broader-suite requirement if applicable
```

Keep the exact Example path in `docs/Hive_Active_Work.md` while verification is pending.
