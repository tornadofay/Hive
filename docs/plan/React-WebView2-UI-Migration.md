# Proposed React + TypeScript + WebView2 Migration for Hive's Own UI

**Status:** Proposal / planning only — not authorized for implementation.  
**Assessment date:** 2026-10-24  
**Scope:** Replace Hive's own Windows-app presentation progressively while preserving the existing Hive .NET backend and the WinForms host-integration capabilities.  
**Authority:** This document is subordinate to AGENTS.md, docs/architecture.md, the relevant architecture detail documents, the roadmap, and Active Work. It creates no roadmap phase, does not advance Phase 1.20, and does not authorize implementation.

## 1. Objective

Move Hive's own user-facing desktop experience from a predominantly WinForms-rendered UI to a modern React + TypeScript interface hosted by WebView2 inside the existing .NET 10 Windows application.

The desired visual/product direction is a **dense desktop IDE-style structure combined with clean, modern card-based content**:

- compact navigation and conversation/history on the left;
- a first-class central Workspace for conversations and supported work;
- an optional, collapsible context panel for the selected target, runtime, task, or other relevant context;
- cards only when they improve grouping or scanning;
- consistent typography, spacing, borders, surfaces, focus/hover/disabled states, icons, and light/dark themes;
- responsive resizing, sensible keyboard behavior, accessibility, and DPI-safe operation.

This is a presentation-layer migration, not a rewrite of Hive's agent platform, execution engine, management model, persistence model, or host-integration architecture.

## 2. Current checkpoint and implications

The repository's current status and verification records are authoritative; the following is a planning checkpoint, not new verification:

- Phase 1.20 Slice 1 (Workspace foundation) and Slice 2 (Chat UX redesign) are recorded as closed.
- Phase 1.20 Slices 3–4 are not started and require their own explicit authorization.
- Active Work currently says there is no active task. The 2026-10-24 Host/UI maintenance closure records 818/818 tests passing, the relevant UI projects building without warnings/errors, and a developer-reported manual smoke check.
- src/Hive.Host.WinForms owns the current Workspace window/shell and host composition, including HiveWorkspaceView, HiveWorkspaceContext, HiveWorkspaceLauncher, and the module boundary.
- src/Hive.Host.WinForms.UI contains the reusable Hive WinForms presentation controls and theme foundation.
- src/Hive.Example.WinForms is a consumer/example host and must remain a consumer rather than become a platform dependency.
- The current Workspace reaches application operations through IHiveManagementFacade; the architecture requires Management to own application operations and the Workspace to remain a presentation/control surface.
- Existing WinForms host-context and bounded business-app integration capabilities have a different purpose from Hive's own UI. They must not be discarded merely because Hive's own screens adopt web technologies.

Relevant sources:

- docs/Hive_Current_Status.md
- docs/Hive_Active_Work.md
- docs/plan/Phase1/1.20.md
- docs/plan/Phase1/1.21.md
- docs/architecture.md
- docs/architecture/foundations.md
- docs/architecture/v1-host-and-management.md
- docs/architecture/v1-business-app-integration.md
- docs/ui/README.md, docs/ui/controls.md, and docs/ui/forms.md
- docs/verification/maintenance/hive-winforms-ui-layer-a-token-normalization-closure-2026-10-24.md
- docs/verification/maintenance/host-ui-deeper-polish-workspace-crud-dpi-2026-10-24.md

## 3. Proposed target architecture

The default proposal is to **retain the existing WinForms desktop host initially** and embed the new React UI through WebView2. Do not migrate to WPF solely to host React; both frameworks can host WebView2, and a WPF migration would add work without materially improving a Workspace rendered by React.

Conceptual ownership:

1. **React + TypeScript frontend** — renders Hive's own Workspace and management screens; owns presentational state, navigation, composing/rendering messages, client-side validation feedback, and interactive visual components.
2. **WebView2** — renders the compiled local frontend inside the Windows application.
3. **Thin WinForms host / bridge** — owns desktop window lifecycle, WebView2 lifecycle, approved native interactions, and a small explicit message-routing boundary. It must not become a second application/business-logic layer.
4. **Existing Hive application and Management services** — remain authoritative for resource operations, policy, validation, authorization, conversation/work state, provider selection, and consequential actions.
5. **Existing persistence/provider/agent/coordination layers** — remain behind their current ownership boundaries. The frontend does not connect to databases, provider endpoints, or secret storage directly.

The exact frontend project/folder and the ownership split between future web presentation assets and the existing Hive.Host.WinForms.UI project must be decided during the architecture/integration spike. Do not silently repurpose or delete the existing UI project. The architecture documents must be updated under an explicitly authorized implementation task if the spike changes their current ownership statements.

### 3.1 Suggested runtime flow

User interaction in React → versioned, typed WebView2 message → allowlisted C# application-facing handler → existing Management/application service → typed result or progress/event message → React updates the relevant component.

Messages must represent specific supported commands and events. The bridge must not expose arbitrary method invocation, reflection, service objects, SQL, private host types, secret-store objects, or unrestricted control of a host business application.

Where the current app uses HiveWorkspaceContext or another lifecycle-aware composition boundary, the migrated Workspace must continue to respect service-graph replacement and disposal. A long-lived modeless window must not retain and call a retired Management facade after host recomposition.

### 3.2 Message contract and execution behavior

The bridge design must settle these details before feature migration:

- A versioned envelope with message kind, stable command/event type, request/correlation identifier, and typed payload.
- An explicit allowlist of commands and event types. Unknown versions, unknown commands, invalid payloads, malformed JSON, over-size messages, and invalid state transitions fail closed with typed, safe errors.
- Asynchronous operations must support completion, cancellation where the owning operation supports cancellation, progress/status events where the backend provides meaningful evidence, and terminal success/failure/cancelled outcomes.
- Progress values must not be invented. If an operation cannot report a meaningful percentage, show an indeterminate state and a truthful status.
- Errors shown to users must be useful and safe. Diagnostic detail must not expose credentials, tokens, connection strings, secret material, or sensitive provider responses.
- Event subscriptions must have explicit ownership and deterministic removal during navigation, WebView disposal, host recomposition, and shutdown. Stale events must not update a disposed or replaced view.
- Concurrent UI commands must follow existing Management concurrency and authorization contracts; the UI must not implement a competing authorization system, durable state machine, retry/fallback policy, or provider-selection policy.

### 3.3 Frontend build and application packaging

Proposed development tooling is a small React + TypeScript project with a conventional frontend build tool such as Vite. The first slice must prove the packaging and debugging approach before that choice becomes binding.

- JavaScript/TypeScript tooling is a developer/build dependency only. End users must not need Node.js, npm, React, TypeScript, or Visual Studio installed.
- Production frontend assets must be built and packaged with Hive. Normal operation must not depend on an internet-hosted page or a running development server.
- Prefer serving bundled assets from a controlled WebView2 virtual host mapping rather than granting the page broad local-file or filesystem access.
- Disallow arbitrary remote navigation and define a safe policy for external links, downloads, clipboard access, file selection, and native dialogs.
- Apply a restrictive Content Security Policy and expose only capabilities needed by the UI.
- The Evergreen WebView2 Runtime is the default deployment candidate. The installer/runtime availability story must be verified, including clear failure messaging when the runtime is absent or cannot initialize.
- Decide how Visual Studio F5/debugging, frontend hot reload, dotnet build, solution build, targeted dotnet test, and publish/package coordinate. Existing backend tests must remain runnable without a frontend development server. The integrated application and release package must fail clearly if required built assets are missing.
- Keep the normal end-user installation experience self-contained: one Hive installation path, with prerequisites acquired or installed through the supported installer flow rather than left as manual development-tool instructions.

The migration must not introduce an unbounded number of browser instances. Use a bounded WebView lifecycle and lazy feature loading where it reduces startup and memory cost without compromising correctness.

## 4. Migration boundary

### 4.1 In scope for current-state parity

Port behavior that is actually implemented today in Hive-owned screens, including applicable existing behavior for:

- Workspace shell and module navigation;
- Direct LLM target selection, favorites filtering, conversations/history, message submission and display, loading/running/failure presentation, and supported theme behavior;
- existing Work Items operations and status presentation;
- Settings/overview navigation and existing CRUD/editor workflows;
- Provider and ProviderAccount configuration and credential status/replacement flows;
- ExecutionTarget configuration, favorites, and related pickers;
- existing Agent definition/settings screens and capability/configuration presentation;
- model discovery, filtering, and model-information details;
- persistence configuration for Embedded and SQL Server;
- SQL Server instance discovery and connection diagnostics;
- bidirectional full-data migration configuration, preflight, progress/status, and failure/completion reporting;
- shared validation, confirmation, error/details dialogs, empty/loading states, navigation and focus behavior, resizing, themes, accessibility, and DPI behavior supported by the current application.

The inventory must be reconciled against actual screens, call sites, and existing automated/manual acceptance paths before each corresponding area is considered migrated. The list above is a checklist to investigate, not a claim that every behavior is implemented identically or can be ported without changes.

### 4.2 Explicitly preserve outside the replacement UI

- All existing C# domain, Management, provider, agent, coordination, execution, persistence, authorization, validation, and durable-state behavior unless a separate authorized requirement explicitly changes it.
- Existing public contracts and compatibility semantics.
- WinForms host integration, bounded host-context discovery, business-app control/data-surface adapters, and reusable host-facing types still used by integrated WinForms applications.
- Hive.Example.WinForms as an independent consumer/example host with its established navigation and exact example/test handoff conventions.
- SQL Server and Embedded persistence parity and the existing secret-protection boundaries.
- The distinction between Hive resources and the user's host-application business data.

### 4.3 Out of scope

- Rewriting Hive.Core, Hive.Management, Persistence, provider transport, agent orchestration, business rules, or authorization in JavaScript.
- Moving database access, provider credentials, or secrets into browser code or browser storage.
- Replacing the WinForms host with WPF, Tauri, Electron, or another desktop shell as part of this plan.
- Replacing the user's actual WinForms business-app UI with React or making host applications run inside WebView2.
- Adding new Agent/Manager/Hive modes, orchestration behavior, business-app write capability, or other roadmap features merely because the new UI could display them.
- Treating unstarted Phase 1.20 Slices 3–4 or Phase 1.21 Agent interaction as already implemented. Those features remain under their owning authorized roadmap work; when implemented, their UI can be added through their own acceptance scope.
- Deleting historical UI controls or documents before their callers and architectural role have been verified.

## 5. Proposed implementation sequence — 12 migration slices

These are proposed planning slices only. They are not current Hive phase/slice identifiers and cannot be started by using a continuation command. Before implementation, the proposal must be accepted, assigned the correct roadmap/Active Work authorization, and have affected architecture documents and project ownership boundaries resolved.

### Slice 1 — Architecture and integration spike

**Purpose:** Prove that the hybrid approach can be built, run, debugged, packaged, and secured within the current solution before investing in feature porting.

Scope:

- Select the frontend source/build folder and define ownership relative to Hive.Host.WinForms, Hive.Host.WinForms.UI, and the example host.
- Host a small local React/TypeScript view in WebView2, with a controlled local asset origin.
- Demonstrate one UI-to-C# command and one C#-to-UI event using a versioned message envelope.
- Verify initialization/disposal, failure messaging, fixed-origin/navigation handling, and one asynchronous sample request.
- Exercise proposed development and production asset workflows in Visual Studio.
- Record the final architecture choice and revise affected architecture documents under the authorized scope.

Acceptance:

- Debug and production paths both load bundled/local UI content without relying on remote web assets.
- Messages cross the bridge only through explicitly registered command/event handlers; no arbitrary host invocation is available.
- Invalid messages and navigation fail closed.
- Missing assets or unavailable WebView2 produce a useful failure instead of a blank or hung window.
- The selected approach is documented, including frontend tooling, project ownership, asset paths, and exact developer build/test commands.

Stop/go gate: do not proceed to a broad migration if this slice exposes an unacceptable security, packaging, debugging, memory, or maintenance problem. Reassess the approach before further work; do not silently switch frameworks.

### Slice 2 — Shared design system and interaction primitives

**Purpose:** Establish one durable visual language before porting many screens.

Scope:

- Define design tokens for typography, spacing, surfaces, borders, radii, semantic colors, elevation, and motion.
- Implement reusable buttons, text inputs, selectors, cards, tabs, navigation, empty/loading/error states, confirmation/details dialogs, progress and status presentation.
- Define light/dark/system themes and behavior when the theme changes while a view is open.
- Define validation display, keyboard focus, disabled/busy states, responsive panel rules, and accessibility semantics.

Acceptance:

- Tokens and primitives are shared rather than duplicated per screen.
- Light, dark, and system theme paths are exercised.
- Keyboard focus order and accessible labels/states exist for the initial primitives.
- Components do not become generic boxes around every content element; the approved desktop-dense/card-selective direction remains intact.

### Slice 3 — Workspace shell and navigation

**Purpose:** Replace the existing Workspace shell while preserving module and application boundaries.

Scope:

- Introduce compact left navigation/history, central content, and an optional collapsible right context area.
- Preserve the current independent/modeless Workspace launch behavior and supported embedding scenario.
- Preserve lazy module creation and module navigation semantics, adapting the module boundary only where required by the web presentation architecture.
- Define browser-history/deep-link behavior only where useful and compatible with the desktop app; browser navigation must not become an authority boundary.

Acceptance:

- Workspace launch/reuse and host embedding behave as before.
- Resizing and supported compact sizes do not clip or strand controls.
- Navigation switches views without leaking handlers or stale state.
- Management recomposition and application shutdown do not leave the frontend calling a retired service graph.

### Slice 4 — Direct LLM experience

**Purpose:** Reach parity for the implemented Direct LLM surface.

Scope:

- Target picker including saved-favorites filtering and current error/empty behavior.
- Conversation navigation/history and current conversation state behavior.
- Composer, send behavior, message rendering, and current request lifecycle/error handling.
- Apply the design system to loading, disabled, validation, empty, and failure states.

Acceptance:

- The same eligible targets are selectable as before; favorites cannot silently broaden the permitted target list.
- Submission continues to name the exact selected ExecutionTarget and uses the existing Management boundary.
- Conversation ownership, concurrency conflicts, persistence, restart/interrupted-state behavior, and failure semantics stay authoritative in Management.
- No direct provider calls, direct persistence access, secret resolution, or new fallback policy is implemented in the frontend.
- Existing Direct LLM scenarios pass, including switching conversations and themes where applicable.

### Slice 5 — Work Items surface

**Purpose:** Port the current Workspace Work Items module without adding future-phase behavior.

Scope:

- Preserve existing WorkItem listing, navigation, status, details, and actions that are currently implemented.
- Render status/activity with consistent loading, empty, validation, authorization, stale-version/concurrency, and failure feedback.
- Preserve existing paging, ordering, and refresh behavior where defined by public Management contracts.

Acceptance:

- Every existing user action has an explicit mapped command and a truthful result state.
- WorkItem identity, ownership, provenance, lifecycle, and authorization remain managed by existing application services.
- No additional approval, business-write, Review, Agent, or collective-work capability is introduced if not already implemented and authorized.
- Existing Work Items examples and tests cover preserved behavior.

### Slice 6 — Provider and account management

**Purpose:** Migrate existing provider setup and advanced Provider/ProviderAccount administration screens.

Scope:

- Provider onboarding and credential replacement/status presentation.
- Provider, account, and credential CRUD supported by current Management operations.
- Explicit validation and safe presentation of operational errors.
- Preserve built-in provider catalog semantics and existing simple-versus-advanced configuration boundaries.

Acceptance:

- Secret material is delivered only to the appropriate C# / Management path and protected by the existing secret-store policy.
- Secret values are not placed in localStorage, sessionStorage, URLs, browser logs, or diagnostic messages.
- Existing provider/account identities, lifecycle, ownership, and validation behavior remain unchanged.
- No direct transport or database implementation is placed in frontend code.

### Slice 7 — Execution targets, favorites, and existing Agent configuration

**Purpose:** Migrate existing execution and Agent management screens.

Scope:

- ExecutionTarget CRUD, capability/settings presentation, and existing selection controls.
- Favorite target editing and persisted semantics.
- Agent definition/settings and configuration screens that exist today.
- Clear display of provider/model/deployment metadata where currently supported without making display strings authoritative identifiers.

Acceptance:

- Exact durable target identities remain authoritative; model names or labels are never used as selection keys.
- Existing capability-aware target selection and validation remain owned by the application boundary.
- Agent configuration operations use current Management contracts.
- New Agent interaction modes or Phase 1.21 behaviors are not pulled into this slice unless their owning phase has been separately implemented and authorized.

### Slice 8 — Model discovery and information

**Purpose:** Port the read-only model discovery and information experience.

Scope:

- Provider/account selectors and current discovery/refresh behavior.
- Model catalog filtering and selected-model details.
- Preserve common decision data, reported capabilities/modalities/limits/pricing, operational evidence, and current Unknown/missing metadata distinctions.

Acceptance:

- Filtering and details use existing authoritative normalization/filter rules rather than reimplementing them independently in JavaScript.
- Missing pricing is not represented as free, and missing capability evidence is not transformed into unsupported or supported evidence.
- Model Information remains observational and read-only.
- Unavailable, stale, failed, and partial discovery states remain distinguishable where currently represented by the backend/UI contract.

### Slice 9 — Persistence configuration

**Purpose:** Port current database setup and persistence settings.

Scope:

- Embedded and SQL Server mode selection and appropriate endpoint/path fields.
- Readiness/status, explicit connection test and initialization actions, and current diagnostics.
- SQL Server instance discovery progress/completion/failure/incompleteness, refresh, and manual Custom selection.
- Preserve explicit host recomposition after configuration changes.

Acceptance:

- Merely opening the page does not perform destructive initialization or apply migration.
- Inspection and connection tests retain current non-creating semantics.
- UI never constructs SQL/SQLite connections or embeds persistence logic.
- Failed configuration or failed candidate graph creation does not replace the currently working service graph.
- Credential/connection diagnostics never expose secrets.

### Slice 10 — Full-data migration UI

**Purpose:** Port the existing bidirectional SQL Server ⇄ Embedded migration workflow.

Scope:

- Source and destination endpoint editors, preflight/readiness, migration progress, and terminal results.
- Existing source/destination distinction, all-data scope, confirmation, failure details, retry/recovery affordances, and active-backend activation semantics.
- Progress and cancellation presentation according to the actual migration contract; do not imply cancellation is supported if the operation does not support it.

Acceptance:

- Source remains unchanged and destination/preflight semantics stay identical to the existing backend contract.
- Migration does not silently change the active backend.
- The UI does not introduce partial-data migration or live synchronization.
- Errors identify the relevant endpoint and actionable diagnostic without exposing connection strings, credentials, or secret material.
- Existing restart durability, secret re-protection, and destination-verification guarantees are not weakened by the new presentation.

### Slice 11 — Remaining screens and complete parity audit

**Purpose:** Close gaps outside the first migrated feature areas.

Scope:

- Inventory remaining current Hive-owned screens and user-facing interaction paths.
- Port remaining Settings/CRUD/editor pages and shared dialogs not already covered.
- Complete shared error, confirmation, accessibility, focus, keyboard, resize, DPI, clipboard, and theme handling where applicable.
- Decide which native dialogs and Windows-specific interactions should remain native versus become web-rendered based on usability and accessibility requirements.
- Identify every remaining use of legacy Hive-owned presentation and classify it as deliberately retained, still awaiting migration, or safe for removal.

Acceptance:

- The inventory is reconciled against actual project/source/call-site evidence; no screen is deemed complete merely because a similarly named React page exists.
- Each current screen has an explicit acceptance path and a named owning frontend feature.
- Existing WinForms host-integration controls/contracts are retained wherever real consumers need them.
- No old and new implementation both own the same business operation or maintain competing durable state.

### Slice 12 — Integration, release, and retirement gate

**Purpose:** Prove the integrated application is deployable and that migration has not silently lost behavior.

Scope:

- Run the full current Hive.Tests suite and focused tests required by changed application/bridge contracts.
- Run frontend type-check, frontend component tests, and production frontend build.
- Build affected .NET projects and the full solution using the finalized developer workflow.
- Manually verify the actual app from Visual Studio, including light/dark/system themes, launch/reuse, resizing, keyboard focus, navigation, representative CRUD, provider/model operations, persistence setup, migration presentation, async events, error paths, and disposal/recomposition.
- Verify publish/install on a clean or representative Windows environment with the supported WebView2 deployment model.
- Remove obsolete Hive-owned UI implementations only after parity and all references have been demonstrated; retain UI/control contracts still required for WinForms business-app integration.

Acceptance:

- Required backend and frontend checks pass; no known high-impact parity, security, lifecycle, accessibility, or packaging blocker remains.
- No required feature silently disappeared or changed semantics during porting.
- Frontend assets are included in release output and the app runs without Node.js/npm or a development server.
- WebView2 runtime absence, initialization failure, missing assets, backend errors, cancellation, and app shutdown are handled predictably.
- The verification record states what the developer actually ran and what was manually verified. Inspection is not recorded as an executed test.

## 6. Feature-parity register required before and during implementation

Each migrated feature must have a small parity row in the implementation task/verification evidence with at least:

- current source screen/module and the existing public Management/application operation it calls;
- user-visible behavior and supported navigation paths;
- normal, empty, loading, invalid, denied, failed, cancelled, and stale/concurrent outcomes where applicable;
- current theme, resize/DPI, keyboard, focus, and accessibility obligations;
- existing automated tests and exact manual/example scenario;
- new frontend/bridge tests needed;
- explicit disposition: migrated, deliberately retained native, not currently implemented, or deferred to its owning future phase.

Do not create a second permanent UI test inventory in a general status document. Put durable acceptance criteria in the owning plan and actual verification in the appropriate verification record.

## 7. Verification model and developer workflow

The implementation must preserve the repository's developer-run verification gate.

For each implementation slice:

1. Read the source-of-truth workflow/architecture and inspect the precise affected screens, service call sites, contracts, test paths, and consumer examples.
2. Establish a behavior baseline before changing the feature.
3. Implement the frontend and bridge only for the authorized feature boundary.
4. Run the targeted frontend checks and .NET tests/builds required by the slice; do not claim a check passed unless the developer/tool actually ran it.
5. Launch the real Windows app and manually verify the slice on its stated example/test path.
6. Audit parity and the actual diff, then record missing evidence or failures. Do not advance while the current slice is verification-pending.
7. Close the slice only when required evidence is present or a specific developer-approved waiver is recorded under repository rules.

The initial 818/818 test result is a historical checkpoint, not a permanent expected count. Re-establish the baseline from the current main branch immediately before implementation. Test counts can legitimately change as authorized features and tests are added.

### Required developer workflow outcomes

- Existing backend-focused tests should not require a running Vite development server.
- Frontend source changes must be type-checked and tested with locally installed, lockfile-pinned dependencies.
- Integrated application builds must include the corresponding compiled static assets.
- A fresh clone must have documented, repeatable instructions to restore frontend dependencies, build, test, run in Visual Studio, and publish/package.
- End users must not install development tools. The supported install/package flow handles the WebView2 runtime requirement.
- The project's warnings-as-errors behavior remains in force for .NET compilation.

The exact command names and whether Visual Studio/MSBuild automatically invokes the frontend build must be finalized by Slice 1 and documented once, rather than allowing multiple undocumented build paths.

## 8. Security, reliability, and performance requirements

### Security

- Serve only the trusted bundled application UI by default; block unapproved navigation and unexpected content origins.
- Validate message origin, version, type, payload shape, identifiers, size, and request lifecycle at the host boundary.
- Use an allowlisted dispatcher; never expose reflection, arbitrary method invocation, filesystem access, SQL, private C# objects, raw secrets, or general host controls.
- Apply the same authorization rules as current Management/service operations. Visual state and UI confirmation are not authorization.
- Never put provider/API secrets or credential-bearing connection strings into browser storage, URLs, telemetry, console output, or unredacted messages.
- Limit native capabilities to explicit operations and validate paths/selections at the owning backend/host boundary.

### Reliability and lifecycle

- Async operations must not freeze either the WebView or the WinForms message loop.
- Model request correlation, cancellation, terminal events, duplicate sends, stale responses, errors, host recomposition, WebView reload, and disposal explicitly.
- Do not silently retry a different ExecutionTarget or change existing failure semantics.
- Ensure a disposed window cannot receive UI callbacks or retain services/handlers indefinitely.
- Preserve existing authoritative persistence/recovery behavior rather than making browser state durable by convenience.

### Performance and resource use

- Treat the user's current 8 GB RAM development machine as an important practical constraint.
- Avoid multiple WebViews for independent panels unless a proven need exists; prefer a single bounded Workspace WebView with frontend module/component routing.
- Keep heavy screens/lists lazy where current behavior already supports lazy creation, and preserve bounded pagination.
- Measure cold start, idle memory, workspace navigation, representative large result/message display, and resize/theme responsiveness during the spike and release gate.
- Avoid premature optimization; compare representative behavior against the current application and fix demonstrated regressions.

## 9. Principal risks and mitigations

| Risk | Mitigation |
|---|---|
| UI looks modern but behavior silently disappears | Feature-parity register, feature-by-feature migration, live acceptance checks, no large-bang replacement. |
| Bridge grows into a second backend | Explicit command/event allowlist; handlers route to existing application/Management boundaries only. |
| Security boundary is weakened by web content | Local bundled assets, constrained navigation/CSP, message validation, minimal native capabilities, secrets kept out of browser storage. |
| Frontend build breaks the existing .NET workflow | Prove exact build/test/debug/package behavior in Slice 1; keep backend-only test paths independent of the development server. |
| Existing architecture ownership becomes contradictory | Resolve project/component ownership in Slice 1; update authoritative architecture only within a future authorized implementation task. |
| Migration loses WinForms host-integration utility | Inventory consumers and retain host-facing WinForms types/contracts independently from Hive's own screen implementation. |
| Memory/startup regression on modest machines | One bounded WebView by default, lazy modules, bundle analysis, cold-start/idle-memory measurement. |
| Existing future roadmap work is accidentally pulled into migration | Port only implemented behavior; route unimplemented features to their owning phase and authorization. |
| Old and new UIs are maintained indefinitely | A staged coexistence window per feature, explicit retirement gate, one long-term owner for each operation. |

## 10. Effort estimate and confidence

Planning estimate for one developer using AI-assisted implementation and developer-run verification:

- **Workspace-only migration:** approximately 4–6 slices, medium-to-high complexity.
- **Full current Hive-owned UI parity:** approximately 10–14 slices; this document proposes 12 slices.
- **Indicative elapsed effort:** about 3–6 weeks for the full migration, assuming regular work, a successful Slice 1 spike, and no major hidden coupling. This is a rough planning estimate, not a commitment or measured delivery forecast. Complex UI parity, browser/host lifecycle issues, packaging, or accessibility gaps could extend it.

The first slice is intentionally a go/no-go gate. After it exposes actual build, host, bridge, packaging, and memory costs, revise the estimate before authorizing broad feature migration if the evidence materially changes the assumptions.

## 11. Decisions required before implementation authorization

The user/repository owner must explicitly approve or revise these choices:

1. Adopt React + TypeScript + WebView2 for Hive's own UI, with the existing WinForms shell retained initially.
2. Confirm the migration boundary: Workspace first versus all currently implemented Hive-owned management screens.
3. Approve the proposed 12-slice sequence or revise it after the spike.
4. Approve the bridge/security design and the frontend/project ownership decision.
5. Decide how the migration is represented in the roadmap and how existing Phase 1.20/1.21 work is sequenced. This plan must not advance, replace, or close those phases by itself.
6. Authorize Slice 1 through the repository's normal Active Work and phase/slice workflow.

Until those decisions are made, this document is planning material only. It does not authorize coding, dependency additions, solution/project changes, migration of current screens, removal of WinForms presentation code, roadmap movement, or changes to Active Work.
