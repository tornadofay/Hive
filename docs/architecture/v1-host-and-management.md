# Hive Architecture — V1 Host, Workspace, Management, and Example Application



This document is part of the authoritative architecture defined by `docs/architecture.md`. It contains the V1 host/business-app boundary, Workspace/Management surface, and WinForms Example/Settings architecture.



## 4. V1 Document & Business-App Integration

The V1 forcing function is the complete pipeline from the first supported input image to a governed business-app write. The first V1 input is intentionally an image; additional document formats are additive later capabilities.

```
Submitted image
        ↓
image preparation / vision routing
        ↓
structured extraction
        ↓
typed candidate record
        ↓
validation
        ↓
write Tool
        ↓
PendingApproval
        ↓
Approve / Reject
        ↓
business-app result
```

The write remains a governed Tool. Hive's database is never a direct gateway to the host application's business database.

### 4.1 Dual Business-App Integration Contract

V1 supports **both API/service integration and bounded UI integration**. They are not mutually exclusive, and a single WorkItem or operation may use the API, the UI, or both.

**API/service path:** a write Tool may call the real application's supported API/service directly when that capability is available.

**UI path:** a bounded UI integration may inspect or operate on the actual host application's UI when needed, including when no usable API exists. The contract is defined against the real WinForms host rather than a universal UI abstraction.

For the V1 WinForms boundary, discovery can cover the Form hierarchy, `Form` instances, `UserControl` instances, `Control`-derived and custom controls, container controls such as Panels and GroupBoxes, nested descendants, and relevant runtime/data-source context. This discovery exists to provide bounded host context to the Agent; it does not grant permission to click, edit, invoke, or otherwise mutate controls.

API and UI integration may therefore be combined within one workflow—for example, using an API for data retrieval and a UI path for a host operation that has no equivalent API.

Generic cross-host integration remains later. V1 proves the concrete WinForms boundary first, then later phases may generalize proven patterns to other host technologies.


### 4.1.1 WinForms Host Context Contract

Phase 1.13 establishes the concrete WinForms host-context discovery boundary before any UI action capability exists.

The host-facing registration surface is intentionally small:

```csharp
using var registration = hostContext.Register(form);
var snapshot = await hostContext.CaptureAsync(
    registration,
    cancellationToken);
```

The registration retains ownership of the explicitly registered root for the lifetime of the registration. Disposing the registration removes it from the host-context boundary; it does not close, dispose, or mutate the registered WinForms control tree.

Discovery returns immutable metadata snapshots rather than raw `Control` references. A snapshot may describe:

- root and descendant runtime type;
- control name and accessibility text;
- visible/enabled/read-only/focus state;
- bounded screen-independent bounds and hierarchy path/depth;
- relevant container/form/user-control classification;
- bounded data-binding descriptors such as binding property, member, and data-source type.

The discovery boundary must never expose a method that clicks, invokes, edits, sets properties, changes selection, or otherwise mutates a host control. Later action capabilities require a separate explicitly authorized contract.

Traversal rules are contractual:

- explicit registered roots only;
- deterministic child ordering from the WinForms `Controls` collection;
- maximum depth and maximum node count are configurable bounded limits;
- cancellation is checked during traversal;
- a visited-reference set prevents duplicate/cyclic traversal;
- a bound breach is reported as a typed discovery limit failure rather than silently returning an apparently complete tree;
- capture does not read arbitrary control state or invoke application code beyond the bounded metadata properties required by the contract.

Registration and capture provenance contains the Hive resource access identity supplied by the host plus a registration/capture identifier and timestamp. Discovery therefore remains attributable to the host registration without granting that registration any authorization to mutate the application.




## 5. Workspace and Management Surface

`Hive.Management` is the authoritative management facade. `Workspace` is the authoritative human-facing operational surface over that facade; host UI code does not bypass the facade for management or persistence operations.

For V1, Workspace is intentionally a small operational surface over Hive.Management. It supports:

- image submission/attachments bound to WorkItems;
- WorkItem status and execution/activity;
- relevant execution/provider status;
- WorkItem notifications;
- pending approvals and the Approve / Reject action for the governed business-app write.

This V1 surface works with a single Agent and does not require Hive membership or Swarm state.

Later Workspace extensions are phase-gated by the capabilities that own their underlying state. These include:

- general **LLM mode** with explicit user model selection;
- **Agentic mode** with Agent/Hive-selected execution targets;
- Agent/Hive organization and topology;
- active Swarm membership;
- Questions, cognitive state, Dreams, and other later-generation views.

For later modes:

- In **LLM mode**, the user explicitly selects the model/execution target subject to normal capability and authorization policy, with a configured default available.
- In **Agentic mode**, the Agent/Hive selects an execution target through the normal Execution Planner and policy boundary. The Workspace displays the selected target and relevant diagnostics, but the user is not required to choose the model for every Agent decision.

Workspace is not a cognitive authority. It displays and controls authoritative Agent/Hive state; it does not invent Agent decisions or rewrite cognitive state outside the normal management/authorization contracts. It displays and controls authoritative Agent/Hive state; it does not invent Agent decisions or rewrite cognitive state outside the normal management/authorization contracts.

A business application can register a host context through a bounded public API such as:

```csharp
ai.Register(this);
```

For V1 WinForms integration, the registered context can expose bounded discovery of the complete relevant Form/control hierarchy, including UserControls, custom/inherited controls, Panels, GroupBoxes, other container controls, nested descendants, and relevant runtime/data-source context. The discovery boundary is structural/contextual and must be cycle-safe, bounded, cancellable, and read-oriented unless a separate action is explicitly authorized.

Registration binds host context to Workspace/Hive management and may reuse an existing specialized Agent. Registration does not by itself create a new Agent, create a Hive, or imply that multiple open forms must communicate. Host-specific specialization and lifecycle are explicit policy/configuration.

After Phase 2 establishes Hive membership and Swarm state, the Workspace can display Agent/Hive organization and the current collaborating subset as a Swarm. The visual representation does not itself create a Hive or Swarm; durable creation and membership follow the Agent/Hive contracts.

V1 management areas:

1. Workspace — V1 operational WorkItem/approval surface
2. Providers / Models / Execution Targets
3. Agents

Later areas are added only when their owning phase lands:

- Hive Membership
- Governance
- Agent/Hive organization and Swarm views
- LLM mode / Agentic mode
- Cognition / Dreams / Questions / Learning Review
- Knowledge / Skills / Memory
- Storage
- Runtime Diagnostics
- Human Intervention
- Resource Inventory
- Configuration Import / Export
- Generic Host Integration diagnostics

---


### 5.1 V1 WorkItem aggregate and Workspace operations

For Phase 1.11, `WorkItem` is the authoritative durable user-visible aggregate. Its durable state is persisted as an event stream with a current snapshot, so status transitions, approval transitions, optimistic concurrency, activity, and outbox work share one transaction.

The V1 application-facing Management contract exposes:

- create an image-backed WorkItem;
- read/list WorkItems within the caller's ownership/scope;
- read WorkItem activity/notifications from the WorkItem event stream;
- read the submitted attachment content through the Management boundary;
- request `PendingApproval`;
- Approve or Reject a pending WorkItem using the expected resource version.

Approval operations are compare-and-set operations against the expected WorkItem version. A stale intervention request returns a typed concurrency error and never applies to a newer WorkItem state. Approve is valid only from `PendingApproval` and transitions to `Completed`; Reject is valid only from `PendingApproval` and transitions to `Rejected`.

The V1 image attachment is immutable input data owned by Hive and bound to exactly one WorkItem. Attachment metadata is part of the WorkItem snapshot; binary content is stored in a dedicated Hive.Persistence table. Creation persists the attachment and WorkItem-created event/snapshot/outbox in the same SQL transaction. Attachment content is bounded and is never exposed through persistence-specific types.

Workspace is a host-facing presentation surface over `Hive.Management`. It does not access `Hive.Persistence`, does not perform SQL, and does not infer or execute business-application writes. In Phase 1.11, execution/provider status is displayed as the currently known WorkItem execution state; actual image-to-extraction-to-write execution is delivered by later V1 pipeline slices.

The generic event store therefore gains only two reusable persistence capabilities needed by this aggregate boundary: listing current snapshots for a resource kind and participating in an existing SQL transaction for an aggregate-specific state change. WorkItem-specific attachment rules remain in the WorkItem persistence owner.



## 13. Example Application

`Hive.Example.WinForms` is a first-class public-API example and verification host, not a temporary demonstration.

Examples are added alongside the features they demonstrate. They use the shared WinForms UI foundation and scalable Category → Subcategory → Example navigation.

The Example host includes developer-oriented test execution tools, but it does not replace `Hive.Tests` or become the authoritative test runner.

Every major public feature example should include:

- normal path;
- relevant failure/recovery path;
- complete copyable public-API snippet;
- expected lifecycle/result.

The V1 example should demonstrate the document-to-business-app pipeline using the same public boundaries available to real hosts.

### 13.1 WinForms UI Foundation

The WinForms visual foundation is a platform-level host concern and is established in Phase 0 so later forms do not independently invent themes, controls, message boxes, spacing, or visual states.

Hive uses a Hive-owned WinForms UI foundation. The current implementation uses native WinForms controls and custom System.Drawing rendering behind `Hive.Host.WinForms.UI`; no third-party rendering package is part of the current solution.

Hive owns the consumer-facing UI contracts and design vocabulary:

- Light / Dark / System theme modes;
- semantic palette, typography, spacing, and common visual-state tokens;
- Hive-specific controls only where Hive needs additional behavior or styling;
- shared controls such as HiveButton and HiveMessageBox where a Hive-owned contract is useful.

The foundation must not become a complete replacement control toolkit. Standard WinForms controls remain valid when their native behavior and Hive styling are sufficient. A Hive-prefixed control is created because Hive needs a contract or behavior, not merely to rename a framework control.

This layer is replaceable: changing the underlying rendering library must not require unrelated forms to change their public Hive UI contracts.

### 13.2 First-Class Example Host
The Phase 0.7 shell is a host-side composition surface, not a Hive platform runtime. It discovers only IHiveExample implementations from designated Example assemblies, builds a Category → Subcategory → Example tree, and replaces one right-side UserControl view at a time. The shell owns view lifetime and navigation state; individual examples own their feature UI and do not register themselves with the shell manually.


`Hive.Example.WinForms` is a permanent developer-facing application used to demonstrate public APIs, inspect platform behavior, and reduce developer friction while building Hive.

The shell uses a left-side Category → Subcategory → Example navigation surface and a right-side replaceable `UserControl`. Nested TabPages are not the primary gallery-navigation mechanism.

Examples implement a small contract such as:

```csharp
public interface IHiveExample
{
    string Category { get; }
    string Subcategory { get; }
    string Title { get; }

    UserControl CreateView(IServiceProvider services);
}
```

Only designated example assemblies are scanned. Adding an example should require implementing the contract, not editing the shell's central registration code.

The Example host may provide a developer test panel that invokes `dotnet test` as an external process against `Hive.Tests`. The Example host must not become an xUnit runner and must not embed xUnit runner internals. Example self-checks may provide immediate feedback but are not authoritative test results.

The Example host is a first-class project from repository scaffolding onward, grows with Hive, and uses the same Hive UI foundation as all other WinForms surfaces.

### 13.3 Settings and Host Runtime Configuration Boundary

Hive Settings is the **global Hive package configuration center** and the permanent first-class user-facing configuration surface for durable Hive-owned package configuration. It is an application-management surface over the same authoritative state consumed by host applications, not a separate test configuration model or a collection of parallel settings roots.

Future durable Hive configuration domains extend this same Settings center only after their owning contract exists. A Settings page must not invent a storage model, policy boundary, or runtime service solely to populate the UI.

The intended configuration path is:

```text
Persisted Hive configuration
        ↓
Application composition root
        ↓
Configured Hive.Persistence services
        ↓
IHiveManagementFacade
        ↓
Host features / execution
```

Settings uses the same public Management boundary:

```text
Hive.Host.WinForms
        ↓
IHiveManagementFacade
```

The Settings UI must not construct SQL connections, execute provider transport, run migrations, or read raw secret material.

#### Persistence bootstrap credential

The Hive database-backed Secret Store cannot supply the SQL password needed to open that same database. Therefore SQL-password persistence configuration requires a separate bootstrap-secret boundary.

The bootstrap credential:

- is stored outside the target Hive database;
- is protected with Windows DPAPI/user scope;
- is referenced by the dedicated `HiveBootstrapCredentialReference` rather than the Hive resource `SecretReference` type;
- is available before Hive.Persistence is constructed;
- is never emitted in ordinary configuration, diagnostics, or Example output.

The bootstrap application boundary supports set/create, replacement, resolution for host composition, and removal only when the persisted persistence configuration no longer references the credential. Raw material is supplied only through `SecretMaterial`; normal configuration reads expose only the bootstrap reference.

After Hive.Persistence is available, Hive-owned resource credentials continue to use the authoritative Hive Secret Store. These two concerns must not be conflated.

#### Agent execution configuration

AgentDefinition configuration may reference an existing ExecutionTarget without duplicating provider/account/endpoint/model data. The ExecutionTarget remains the authoritative source of concrete execution details.

The first configured-host flow may use one explicit configured target reference. More advanced target selection remains owned by the existing execution-target selection architecture.

A configured Agent is therefore actual durable application configuration:

```text
AgentDefinition
    ↓ configured ExecutionTarget reference
Management resolves target
    ↓
ExecutionTarget → ProviderAccount → Provider
    ↓
execution boundary
```

A missing, unauthorized, retired, or otherwise unusable referenced target is a configuration/runtime boundary failure, not a UI-only state.

For the first configured-host Agent operation, the application-facing Management facade may consume the existing Hive.Coordination.AgentExecutionService rather than duplicating execution mechanics in the host or Example project. The Management operation resolves the persisted AgentDefinition, its configured ExecutionTarget, the authoritative Provider and ProviderAccount relationship, and the optional ProviderAccount Secret Store credential through their existing Management/Persistence boundaries, then submits one immutable execution request to the Coordination execution service. It must not copy provider credentials or target configuration into host state. The configured operation resolves the AgentDefinition and target afresh for each request, so a later Settings change affects subsequent executions while an already-running execution keeps its established target/configuration snapshot.

#### Settings UI foundation

Hive.Host.WinForms.UI owns the reusable Settings presentation primitives. Settings must use the established Hive UI foundation rather than introducing another navigation/list system.

For the first-class Settings surface:

- HiveNavigationTree is the navigation control;
- HiveListPageLayout provides list-page composition;
- HiveCrudPage<TItem> and HiveListView provide reusable list/CRUD presentation where their contracts fit;
- HiveEditorLayout provides repeated editor field/action composition;
- IHiveThemeManager remains the only theme authority.

Settings pages remain domain-owned for field semantics, validation, authorization, and Management operations. The reusable controls remain presentation infrastructure only.

#### Host recomposition

Changing Persistence configuration changes the persistence dependency graph and therefore requires host/application recomposition rather than mutating the existing Management facade.

The safe lifecycle is:

```text
current service graph
    ↓
validate/load new settings
    ↓
resolve bootstrap credential through its application-facing contract
    ↓
construct complete candidate persistence-backed stores
    ↓
construct new Management facade
    ↓
publish candidate graph
    ↓
dispose old graph
```

The concrete bootstrap-secret storage mechanism is an infrastructure implementation behind that contract. The composition boundary must not depend on DPAPI/file details. A failed candidate build must leave the currently published graph intact.

Provider/ProviderAccount/ExecutionTarget/AgentDefinition changes do not require persistence graph reconstruction. They require authoritative Management reads and host state refresh.

Running executions use their already-established effective configuration snapshot; later Settings changes do not silently alter an execution already in progress.

The Example Host is the first concrete application-level consumer of this boundary. It exposes the real Hive Settings center through the Overview → Getting Started → Example Configuration leaf, whose primary action opens the host-level Settings window. The Settings UI uses the reusable Hive.Host.WinForms.UI foundation.

The Settings resource hierarchy exposes Providers with three distinct resource-management leaves beneath them, plus Agents and Persistence:

```text
Providers
├── Provider Configuration      → Provider CRUD
├── Accounts / Credentials     → ProviderAccount CRUD, scoped by Provider
└── Execution Targets          → ExecutionTarget CRUD, scoped by Provider + ProviderAccount
Agents                          → AgentDefinition CRUD
Persistence                    → one global configuration editor
```

Provider is the durable provider identity/transport resource. ProviderAccount is a durable credential/resource record, not a provider login screen. ExecutionTarget is the concrete endpoint/model/deployment/capability resource and remains the authoritative target referenced by AgentDefinition. These domains must not be collapsed into one combined form when separate Management CRUD contracts already exist.

Persistence is not a resource collection. It edits one global HivePersistenceConfiguration, so its leaf is intentionally an editor rather than a CRUD page. Its Server / instance control is a free-form text field. It accepts local servers, named instances, remote hosts, IP addresses, and online SQL Server targets. Hive does not currently define an authoritative SQL Server discovery/catalog contract, so the Settings UI does not enumerate installed SQL Server instances. The Database value is Hive-owned and assigned automatically by the Settings surface.

It must use the configured Provider/Account/Target/Agent state in normal public-API examples. It may not construct a competing Hive service graph or bypass the host composition boundary. The configuration example explains this model but is not a substitute for the real Settings surface or configured runtime consumption.

### 13.4 UI Foundation Scope Boundary

The UI foundation is infrastructure, not a mechanism for pulling future platform capabilities into Phase 0. It must not require Hive membership, Swarm, CognitiveAgent, CognitiveHive, Dreams, Questions, or other later-generation behavior.

Phase 1 and later features consume the shared UI foundation instead of creating parallel form/control systems.

### 13.5 UI Design and Responsiveness Contract

The shared WinForms UI foundation is held to application-grade desktop UI standards rather than demonstration-oriented styling. Shared presentation contracts cover hierarchy, spacing, typography, visual density, navigation states, semantic colors, focus/hover/pressed/disabled states, dialog presentation, CRUD presentation, and resize behavior.

Theme changes must preserve user navigation state and must not cause avoidable TreeView scrolling, selection changes, focus loss, or layout jumps. Theme application should update only controls whose effective visual state changes and should avoid unnecessary handle recreation, control-tree traversal, layout passes, painting, or GDI/resource churn.

Responsive behavior is achieved through normal WinForms anchoring/docking/layout containers and bounded minimum sizes. Hive does not introduce a custom DPI system. Shared controls must remain usable when the host window is resized within its supported bounds, including compact desktop dimensions.

UI controls own their disposable fonts, brushes, pens, paths, and child controls and release them deterministically. Theme tokens are stable immutable values and are reused rather than reconstructed by individual consumers.

The Example host is the permanent visual acceptance surface for the UI foundation. Before the 0.7 slice is considered complete, the developer manually verifies Light, Dark, and System modes, navigation state preservation, resize behavior, CRUD presentation, dialogs, focus/hover/disabled states, and consistent rendering across the shared controls.