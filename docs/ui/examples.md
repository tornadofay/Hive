
# Hive.Example.WinForms Development Guide

## Purpose

Hive.Example.WinForms is Hive's permanent developer-facing public-API example and verification host.

It exists to:

1. demonstrate how consumers use Hive public APIs;
2. provide reproducible manual verification scenarios;
3. reduce developer and AI friction while building Hive.

An Example is not a prototype implementation of the feature itself. It is a thin, reproducible consumer of the real public capability.

## Example contract

The current Example Host discovers implementations of this internal contract:

~~~csharp
internal interface IHiveExample
{
    string Category { get; }
    string Subcategory { get; }
    string Title { get; }
    int Order => 0;
    UserControl CreateView(IServiceProvider services);
}
~~~

`IHiveExample` is an Example Host composition contract, not part of Hive's external product API. A scenario is public-facing when the behavior demonstrated by the scenario is exposed through Hive's supported public contracts.

A concrete Example must:

- implement IHiveExample;
- not be abstract;
- have a parameterless constructor;
- return a non-null UserControl from CreateView(IServiceProvider).

The discovery code scans the Example Host assembly at startup and creates concrete types that implement `IHiveExample` and expose a parameterless constructor. Examples are sorted by:

1. `Order`;
2. `Category`;
3. `Subcategory`;
4. `Title`.

Do not manually register a new Example in `HiveExampleHostForm`.

## Example navigation

The host displays:

~~~text
Category
  └── Subcategory
       └── Example
~~~

Choose Category and Subcategory according to the architecture/capability being demonstrated, not merely the implementation class or namespace.

Related scenarios should share a stable category/subcategory.

Do not create a new top-level category simply because the implementation is in a different project.

## Implementing a new Example

Typical pattern:

~~~csharp
internal sealed class ProviderResourceExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Provider Platform";

    public string Title =>
        "Provider / ProviderAccount / ExecutionTarget";

    public int Order => 10;

    public UserControl CreateView(IServiceProvider services)
    {
        return new ProviderResourceExampleView(services);
    }
}
~~~

The discovery class should remain a small metadata/creation shell. Put non-trivial scenario UI and behavior in a focused UserControl/view.

Do not create a giant IHiveExample class containing all UI, service orchestration, validation, and output handling.

## Example View

The returned UserControl is the actual scenario.

Prefer:

- native WinForms controls when sufficient;
- Hive shared controls/layouts when they match the scenario;
- HiveExampleTestSurface for interactive deterministic test scenarios;
- IHiveExampleOutput for scenario output.

Typical structure:

~~~text
Example View
├── explanation/setup
├── test controls
├── expected result
├── Run / Cancel
└── shared Output
~~~

The scenario should state what it demonstrates and what the developer should expect to observe.

## Shared Example services

The current Example Host supplies shared services through IServiceProvider, including:

- IHiveThemeManager;
- IHiveExampleOutput.

Consume these through the established Example Host service contract. Do not access HiveExampleHostForm private fields or create a second global service locator.

When adding a new host-wide shared service, first determine whether it truly belongs in the Example Host composition boundary. Do not add feature-specific services globally.

## Example output

Use IHiveExampleOutput:

~~~csharp
public interface IHiveExampleOutput
{
    void Clear();
    void Write(string title, string value);
    void Append(string value);
}
~~~

Example:

~~~csharp
output.Write(
    "Provider created",
    $"Id: {provider.Id}");

output.Append(
    $"{Environment.NewLine}Provider version: {provider.Version}");
~~~

Never print secrets or credentials. Keep output useful and safe.

## Reproducible interactive scenarios

For a meaningful scenario, prefer HiveExampleTestSurface.

Configure:

- Description;
- ExpectedResult;
- optional NoteTitle/NoteText;
- input;
- C# reproduction snippet;
- cancellation-aware run action.

The run action must call the real public API.

Do not implement a fake success path that only changes the status label.

## Failure and cancellation

Examples are verification surfaces, so meaningful failure cases should remain visible.

Use HiveExampleTestSurface.RunAsync or equivalent shared behavior when applicable so:

- cancellation is supported;
- failures are shown;
- technical details can be written to output;
- busy state is visible;
- controls are disabled appropriately.

Do not swallow exceptions merely to keep the Example looking successful.

## Public API rule

Examples must use the same public contracts an external consumer is expected to use.

Do not:

- call internal production helpers merely for convenience;
- directly mutate persistence tables;
- bypass Hive.Management for management behavior;
- create a private duplicate of a production service;
- use Hive.Tests test-only types as the application implementation.

Persistence integration tests remain the responsibility of Hive.Tests. The Example should demonstrate the corresponding public capability when the active slice defines it as externally usable.

## Testing relationship

For every new meaningful externally usable capability:

~~~text
Focused automated tests
        +
Matching Example scenario
~~~

Automated tests verify deterministic contracts/boundaries.

The Example verifies the externally usable public API path and gives the developer an explicit manual verification target.

The Example does not replace Hive.Tests.

## Example handoff

Every new meaningful capability that requires an Example must produce:

~~~text
Example to run: <exact Category / Subcategory / Example path> — Hive.Example.WinForms
Tests to run: <exact focused test class/file>; broader-suite requirement if applicable
~~~

When verification is pending, docs/Hive_Active_Work.md must preserve the same exact Example path and focused test target.

Do not write only "Run the Example." The checkpoint must tell the developer exactly what to select.

## Manual verification expectations

For an externally usable capability, manually verify the Example when the active slice requires it.

Depending on the capability, verify:

- scenario loads without exception;
- setup is understandable;
- expected result is visible;
- failure behavior is visible and safe;
- cancellation works where applicable;
- output is useful and secret-safe;
- the scenario uses the correct public API;
- light/dark theme remains usable;
- resize/layout remains usable.

## Adding an Example checklist

Before finishing a new capability:

- implement the real production capability;
- add/update focused Hive.Tests coverage;
- add the matching IHiveExample implementation;
- classify it with the correct Category/Subcategory;
- return a focused UserControl;
- use the shared UI/output/testing primitives where appropriate;
- ensure the scenario uses public Hive APIs;
- provide a clear expected result;
- provide the exact Example path in the handoff;
- provide the exact focused test target in the handoff;
- update docs/Hive_Active_Work.md when the verification checkpoint requires it.

## What not to change

Do not edit HiveExampleHostForm merely to add a new Example.

The Host already:

- discovers examples from its assembly;
- builds Category → Subcategory → Example navigation;
- creates one active UserControl at a time;
- themes the newly selected view;
- owns active-view lifetime;
- owns shared output.

Change the Host only when the host/composition contract itself needs to change.
