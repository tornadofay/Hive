# Hive.Example.WinForms Development Guide

## Purpose

Hive.Example.WinForms is Hive's permanent developer-facing public-API example and manual-verification host.

It exists to:

1. demonstrate supported Hive public APIs;
2. provide reproducible developer scenarios;
3. give the developer an explicit manual verification target;
4. reduce developer and AI friction while building Hive.

An Example is not a prototype implementation of the feature itself. It is a thin consumer of the real capability.

## Example Host contract

The current Example Host uses this internal composition contract:

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

IHiveExample is internal to Hive.Example.WinForms. It is not part of Hive's external product API.

The public-facing part of an Example is the Hive capability demonstrated by its view, not the IHiveExample interface itself.

## Discovery rules

HiveExampleDiscovery.Discover(Assembly assembly) scans the supplied assembly and considers concrete types that:

- implement IHiveExample;
- are not abstract;
- are not interfaces;
- expose a parameterless constructor;
- can be instantiated by the Example Host.

The current Example Host supplies Assembly.GetExecutingAssembly(), so the normal implementation location is Hive.Example.WinForms itself.

Discovery is deterministic. Examples are sorted by:

1. Order;
2. Category;
3. Subcategory;
4. Title.

Do not manually register an Example in HiveExampleHostForm.

Adding an Example should normally mean adding the Example composition class and its view, not editing central navigation code.

## Example metadata and navigation

The host displays:

~~~text
Category
  └── Subcategory
       └── Example
~~~

Choose Category and Subcategory according to the capability/architecture being demonstrated.

Related examples should share stable navigation names. Do not create a new top-level category merely because the implementation happens to live in another project.

The current host collapses the navigation after rebuilding it, then expands the parents of the selected example when it chooses the first scenario.

## Typical Example file organization

The current repository commonly uses a pair of files:

~~~text
ProvidersExample.cs
ProvidersExampleView.cs
~~~

The names do not need to be identical in every feature, but keep the metadata/discovery class separate from non-trivial scenario UI and behavior.

## Example metadata class

Use the current internal sealed pattern:

~~~csharp
internal sealed class ProviderResourceExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Provider Platform";

    public int Order => 10;

    public string Title =>
        "Provider / ProviderAccount / ExecutionTarget";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ProviderResourceExampleView(services);
    }
}
~~~

Keep this class small. It should primarily provide metadata and construct the scenario view.

The scenario view owns the actual UI and example behavior.

## Example view

The returned UserControl is the real scenario surface.

Prefer:

- native WinForms controls when they are sufficient;
- Hive shared controls/layouts when they match the scenario;
- HiveExampleTestSurface for reproducible interactive scenarios;
- IHiveExampleOutput for shared output.

Typical composition:

~~~text
Example View
├── explanation/setup
├── test controls
├── expected result
├── Run / Cancel
└── shared Output
~~~

Meaningful scenarios should tell the developer:

- what capability is being demonstrated;
- what setup is required;
- what the expected result is;
- what failure/cancellation behavior looks like when relevant.

## Theme application for Example views

Example Host code creates and themes the next view before it becomes visible:

~~~csharp
var nextView = example.CreateView(_services);
ArgumentNullException.ThrowIfNull(nextView);

_themeManager.Apply(nextView);
_viewHost.Controls.Add(nextView);
~~~

Feature Example views should therefore compose their child controls normally.

When a view creates additional detached/dynamic child controls after the host has already themed the view, apply the same shared IHiveThemeManager to that new subtree.

Do not create a private theme manager just for one Example.

## Shared Example services

The current Example Host supplies:

- IHiveThemeManager;
- IHiveExampleOutput.

Use the existing typed extensions:

~~~csharp
var themeManager = services.GetThemeManager();
var output = services.GetExampleOutput();
~~~

These extensions validate the service boundary and throw clearly when the requested service is unavailable.

Do not:

- reach into HiveExampleHostForm private fields;
- call the concrete output view directly when the interface is sufficient;
- create another global service locator;
- put feature-specific dependencies into the shared host service container unless they are genuinely host-wide.

## Example execution surface

For meaningful interactive scenarios, prefer HiveExampleTestSurface.

Configure:

~~~csharp
surface.SetInformation(
    "What this example demonstrates.",
    "What the developer should observe.",
    "Optional note title",
    "Optional note text");

surface.InputText = "Optional reproducible input";

surface.CodeSnippet = """
// Focused public-API reproduction
""";

surface.ConfigureRun(
    RunScenarioAsync,
    output,
    owner);
~~~

The run action must call the real public Hive API.

Do not replace the real operation with a fake status change such as "Completed" without executing the capability.

## Failure and cancellation

HiveExampleTestSurface provides a standard interactive lifecycle:

- the Run button becomes Cancel while busy;
- Cancel requests cancellation through the operation token;
- OperationCanceledException is presented as Cancelled when it corresponds to the active cancellation;
- other exceptions are marked as Failed;
- exception details are written to the supplied output when one is configured;
- a Hive error dialog is shown when a suitable owning window is available.

The scenario itself should still use proper cancellation-aware public APIs.

Do not catch every exception in the scenario merely to keep the Example visually green.

## Output contract

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

HiveExampleOutputView currently:

- replaces the output on Write;
- prepends a local timestamp on Write;
- appends raw text on Append;
- supports Clear;
- supports Copy;
- can be collapsed/expanded;
- reports output availability and collapse-state changes.

Never print secrets, credentials, access tokens, or unnecessary sensitive provider/business data.

## Public API rule

Examples must use the same supported public contracts that a real consumer host is expected to use.

Do not:

- call private/internal production helpers merely for convenience;
- directly mutate persistence tables;
- bypass Hive.Management for management behavior;
- build a private duplicate of a production service;
- use Hive.Tests types as production integration shortcuts.

When the active slice defines a new public persistence/management capability, the Example should normally demonstrate the corresponding public path. Hive.Tests remains responsible for persistence isolation, malformed-state coverage, concurrency coverage, and other automated contract checks.

## Code snippet quality

The code snippet shown by HiveExampleTestSurface should be useful for reproduction.

For a major public capability, prefer a complete, copyable public-API example that matches the real scenario. For a small UI-only example, a focused excerpt can be sufficient when the surrounding setup is obvious.

Do not write a snippet that claims to reproduce a capability while calling an internal helper or omitting the decisive public operation.

## Testing relationship

For each new meaningful externally usable capability:

~~~text
Focused automated tests
        +
Matching Example scenario
~~~

Automated tests prove deterministic contracts and boundaries.

The Example proves that the supported public API can be exercised through the intended developer-facing surface and gives the developer an explicit manual verification target.

The Example does not replace Hive.Tests.

## Exact handoff

Every new meaningful externally usable capability that requires an Example must produce:

~~~text
Example to run: <exact Category / Subcategory / Example title> — Hive.Example.WinForms
Tests to run: <exact focused test class/file>; broader-suite requirement if applicable
~~~

When verification is pending, docs/Hive_Active_Work.md must preserve the same exact Example path and focused test target.

Do not write only "Run the Example."

The handoff must identify the exact navigation path/title so the developer does not have to rediscover the scenario.

## Manual verification checklist

When the active slice requires manual Example verification, verify the applicable items:

- the Example Host starts and the scenario can be selected;
- the scenario creates/loads without an unhandled construction exception;
- the explanation and expected result are understandable;
- Run / Cancel behaves correctly;
- failure is visible and safe;
- cancellation is honored where supported;
- output is useful and secret-safe;
- the scenario uses the intended public Hive API;
- Light / Dark / System themes remain usable;
- resizing does not break the view;
- dynamic view replacement does not leave disposed controls or stale handlers.

Static inspection of an Example class is not evidence that the scenario was manually verified.

## Adding an Example checklist

Before handing off a new capability:

- implement the real production capability;
- add/update the focused Hive.Tests coverage;
- add the matching IHiveExample implementation;
- choose the correct Category/Subcategory;
- keep the metadata class small;
- return a focused UserControl;
- use shared Hive UI/output/testing primitives where appropriate;
- configure a clear expected result;
- use public Hive APIs;
- provide a useful reproduction snippet for major capabilities;
- preserve failure/cancellation visibility;
- document the exact Example path in Hive_Active_Work.md while verification is pending;
- provide the exact Example/Test handoff lines.

## What not to change

Do not edit HiveExampleHostForm merely to add a new Example.

The current host already:

- discovers IHiveExample implementations from its own assembly;
- builds Category → Subcategory → Example navigation;
- creates one active UserControl at a time;
- applies the shared theme to the new view before it is shown;
- disposes the previous active view;
- supplies the shared Example services;
- owns shared output presentation.

Change the host only when the host/composition contract itself needs to change, such as:

- a genuinely new shared Example service;
- a change to the discovery contract;
- a change to navigation composition;
- a change to active-view ownership/lifecycle;
- a change to shared output behavior.

A feature-specific Example should not force a host-wide change when the existing composition contract is sufficient.
