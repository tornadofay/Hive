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
   └─ Example Title
```

## Tree placement

`Category` and `Subcategory` are the actual TreeView node text.

Current branches:
- `UI → Foundation`
- `Providers → Provider Platform`
- `Providers → Target Selection`
- `Providers → Provider Transport`

For a new Example:
1. Reuse the existing Category/Subcategory that matches the capability.
2. Use the exact existing spelling/casing so the Example appears under that branch.
3. Create a new Category/Subcategory only when no existing branch fits.
4. Do not edit `HiveExampleHostForm`; changing these two properties is enough.

Examples:
- UI control/theme/dialog/CRUD example → `UI / Foundation`
- Provider/ProviderAccount/ExecutionTarget example → `Providers / Provider Platform`
- Capability-aware execution target selection example → `Providers / Target Selection`
- OpenAI-compatible provider transport example → `Providers / Provider Transport`

`Order` controls ordering within the discovered examples. Title is the leaf text.

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
Example to run: <Category / Subcategory / Example title> — Hive.Example.WinForms
Tests to run: <focused test class/file>; broader-suite requirement if applicable
```

Keep the exact Example path in `docs/Hive_Active_Work.md` while verification is pending.
