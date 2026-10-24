# Proposed React + TypeScript + WebView2 Migration for Hive's Own UI

**Status:** Proposal / planning only — not authorized for implementation.  
**Assessment date:** 2026-10-24  
**Scope:** Add a separate React + TypeScript frontend project/package and a separate .NET 10 WebView2 desktop executable alongside the existing WinForms-based host path. Today, `Hive.Host.WinForms` and `Hive.Host.WinForms.UI` are libraries, while `Hive.Example.WinForms` is the executable consumer/example host; preserve all three existing projects and their implemented behavior. Use the current Hive-owned WinForms presentation as the parity baseline, then make the new React/WebView2 application the primary standalone Hive-owned UI only after parity and architecture alignment are formally approved. Preserve the existing backend and business-app host-integration capabilities.  
**Authority:** This document is subordinate to AGENTS.md, docs/architecture.md, the relevant architecture detail documents, the roadmap, and Active Work. It creates no roadmap phase, does not advance Phase 1.20, and does not authorize implementation.

## 1. Objective

Build a separate React + TypeScript frontend project/package and a separate thin .NET 10 WebView2 desktop executable as a new application path alongside the current Example Host. Prefer a separate WinForms executable host that uses WebView2 for application content and the existing public `HiveForm` shell for native desktop chrome. It must not display the legacy Workspace/Settings forms as the React application's content. Reuse existing Hive application/Management behavior and host composition; Slice 1 must resolve the current co-location of composition and presentation without duplicating the service graph.

The existing WinForms projects must remain intact and buildable during migration. They are retained as a preserved implementation and fallback/reference, not a second UI that must receive every future feature. When React has demonstrably reached parity with the current implemented WinForms UI, make React the primary Hive-owned UI and put the old WinForms presentation on hold. Future Hive-owned UI development proceeds in React; the WinForms UI receives only explicitly authorized compatibility, build, or security corrections needed to preserve its retained value, not routine feature development or ongoing polish.

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
- `src/Hive.Host.WinForms` is currently a `net10.0-windows` library that owns the Workspace/Settings presentation and host/service-graph composition, including `HiveWorkspaceView`, `HiveWorkspaceContext`, and `HiveWorkspaceLauncher`.
- `src/Hive.Host.WinForms.UI` is a separate library that owns reusable WinForms controls/theme, including the public `HiveForm` shell and the internal `HiveWindowHeader` it composes.
- `src/Hive.Example.WinForms` is the current executable consumer and verification host. It must remain a usable example/consumer, not become a platform dependency. The plan must not incorrectly describe `Hive.Host.WinForms` itself as an existing executable application.
- The current Workspace reaches application operations through `IHiveManagementFacade`; Management owns application operations and the Workspace remains a presentation/control surface. The current architecture supports both a modeless single-instance Workspace window and an embeddable `HiveWorkspaceView`; that embedding behavior is part of the baseline and must be preserved or explicitly dispositioned before the React parity gate.
- Existing WinForms host-context and bounded business-app integration capabilities have a different purpose from Hive's own UI. They must not be discarded merely because Hive's own screens adopt web technologies.

### Architecture alignment prerequisite

The current canonical architecture (`docs/architecture.md`, **Standalone preview product boundary**) defines the first public Hive Workbench as a self-contained WinForms application that reuses the Phase 1.20 Workspace and `Hive.Host.WinForms.UI`. This plan proposes a materially different future target: a separate React/WebView2 application becoming the primary standalone Hive-owned UI. That change is **proposed, not yet adopted**; this plan does not override the canonical architecture.

Slice 1 is therefore also an architecture decision gate. It must reconcile the target Workbench/application boundary, project ownership, and roadmap sequencing with the canonical architecture before the plan proceeds to feature-migration slices. The owner must either approve this target direction and authorize the corresponding architecture/roadmap documentation updates under the normal workflow, or retain the WinForms Workbench target and stop/revise this migration proposal. Do not leave the plan and canonical architecture describing contradictory primary application targets. Until that decision is recorded, Slices 2–12 are not eligible to start.

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

The proposed architecture is **two parallel application paths sharing Hive's existing application/backend capabilities**: the existing WinForms presentation exercised by `Hive.Example.WinForms`, and a new standalone React/WebView2 executable. Do not turn `Hive.Host.WinForms` into the new executable or put React into the existing Workspace/Settings forms. The new host should use the existing public `HiveForm` shell (which composes `HiveWindowHeader`) if Slice 1 proves this integration is clean. `HiveWindowHeader` is internal and must not be made public merely to enable reuse; the EN/AR action needs a narrow, opt-in extension through the existing shell, with all legacy forms retaining their current header behavior by default. Slice 1 must also resolve whether the new host can reuse the current host composition directly or needs a bounded shared composition boundary.

Conceptual ownership:

1. **Separate React + TypeScript frontend project/package** — owns Hive's new Workspace and management-screen presentation, frontend build/test setup, components, client-side interaction state, and static production assets. It has its own package manifest, dependency lockfile, TypeScript configuration, and frontend tests.
2. **Separate WebView2 desktop host project** — a thin .NET 10 Windows executable, preferably a WinForms host that embeds WebView2 in the body of the public `HiveForm` shell. It owns the window/WebView lifecycle, approved native capabilities, local asset loading, and bridge. The existing `HiveWindowHeader` is internal; do not expose or instantiate it across project boundaries directly. Instead, if the EN/AR header control requires a change, add a small, opt-in header action/event through the existing `HiveForm` API (or document a technically justified alternative in Slice 1). Existing WinForms forms must keep their current header buttons/behavior unless they explicitly opt into the new action. Decide the exact project name and host composition dependency in Slice 1.
3. **Explicit C# bridge** — accepts only versioned, allowlisted commands and sends typed results/events; it is not a second service/business-logic layer.
4. **Existing Hive application and Management services** — remain authoritative for resource operations, policy, validation, authorization, conversation/work state, provider selection, and consequential actions.
5. **Existing persistence/provider/agent/coordination layers** — remain behind their current ownership boundaries. The frontend never connects directly to databases, provider endpoints, or secret storage.
6. **Existing WinForms projects** — remain intact and buildable during the migration. After the React application passes the full parity gate, the current Hive-owned WinForms presentation is put on hold as a preserved fallback/reference. Host-integration contracts and controls with real consumers remain supported. Preserve the current legacy app's header behavior; the EN/AR selector belongs to the new React application's native header path and must not become ongoing feature work for the frozen legacy UI.

Host composition and screen presentation are currently co-located in `Hive.Host.WinForms`, which references `Hive.Host.WinForms.UI`; do not assume the new host can reuse composition while having no project-level dependency on that assembly. In Slice 1 inspect the actual public composition surface and choose the smallest valid route: reuse the existing host library without launching its legacy forms, or establish a narrowly scoped shared composition boundary if dependency direction requires it. Do not duplicate the Management/service graph or make React content depend on legacy presentation types. Keep existing projects/source/history; do not rename, repurpose, or delete them.

### 3.1 Application lifecycle and preservation policy

The migration has two distinct periods:

**Parity period:** the existing Hive-owned WinForms presentation, exercised through `Hive.Example.WinForms`, is the reference baseline and remains buildable with its current user-visible behavior. The React frontend and separate WebView2 executable are developed alongside that host path. Do not port new UI features back to WinForms to keep both implementations synchronized; compare React against the behavior that exists at the agreed baseline. A narrowly scoped, backwards-compatible opt-in header API change is permitted only if required for the new host; all existing forms must retain current header behavior by default. Other legacy UI fixes remain separately bounded to buildability or material correctness/security issues.

**After the parity gate:** React/WebView2 becomes the primary Hive-owned desktop UI and the normal launch/distribution path for new Hive UI use. New Hive-owned UI features are implemented only in React. Existing WinForms-owned presentation projects remain in the solution and their source/history is preserved as a dormant fallback/reference. They are not routinely updated, polished, deleted, or rewritten. Explicitly authorized compatibility, security, or build fixes may be made when necessary to preserve their retained value. The old WinForms UI is not promised feature parity with capabilities added only after the cutover.

This policy applies to Hive's own UI. It does not remove or replace the WinForms host-integration capabilities needed to integrate Hive with users' actual WinForms business applications.

### 3.2 Suggested runtime flow

User interaction in React → versioned, typed WebView2 message → allowlisted C# application-facing handler → existing Management/application service → typed result or progress/event message → React updates the relevant component.

Messages must represent specific supported commands and events. The bridge must not expose arbitrary method invocation, reflection, service objects, SQL, private host types, secret-store objects, or unrestricted control of a host business application.

Where the current app uses HiveWorkspaceContext or another lifecycle-aware composition boundary, the migrated Workspace must continue to respect service-graph replacement and disposal. A long-lived modeless window must not retain and call a retired Management facade after host recomposition.

### 3.3 Message contract and execution behavior

The bridge design must settle these details before feature migration:

- A versioned envelope with message kind, stable command/event type, request/correlation identifier, and typed payload.
- An explicit allowlist of commands and event types. Unknown versions, unknown commands, invalid payloads, malformed JSON, over-size messages, and invalid state transitions fail closed with typed, safe errors.
- Asynchronous operations must support completion, cancellation where the owning operation supports cancellation, progress/status events where the backend provides meaningful evidence, and terminal success/failure/cancelled outcomes.
- Progress values must not be invented. If an operation cannot report a meaningful percentage, show an indeterminate state and a truthful status.
- Errors shown to users must be useful and safe. Diagnostic detail must not expose credentials, tokens, connection strings, secret material, or sensitive provider responses.
- Event subscriptions must have explicit ownership and deterministic removal during navigation, WebView disposal, host recomposition, and shutdown. Stale events must not update a disposed or replaced view.
- Concurrent UI commands must follow existing Management concurrency and authorization contracts; the UI must not implement a competing authorization system, durable state machine, retry/fallback policy, or provider-selection policy.

### 3.4 Frontend build and application packaging

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

### 3.5 Localization, language preference, and bidirectional layout

Localization is an architectural requirement from the start, even if the first production release initially ships with English translations only. Do not postpone the foundations until after screens contain hardcoded English labels and left-to-right assumptions.

- The native desktop header for the new React/WebView2 application must expose an `EN | AR` language toggle. Use the existing `HiveForm` shell and add a narrow, opt-in header action/event where appropriate; do not expose the internal `HiveWindowHeader` type. Existing WinForms forms must retain their current header behavior by default. The toggle must not rebuild desktop window controls in React.
- Selecting a language must update the React UI at runtime without restarting Hive. The native host communicates the selected locale through the versioned bridge; React updates its translation resources, sets the document language (`lang="en"` or `lang="ar"`), and sets direction (`dir="ltr"` for English, `dir="rtl"` for Arabic).
- Store visible UI strings in localization resources with stable keys; do not hardcode user-facing English copy throughout components. Establish the resource format, key conventions, pluralization/interpolation needs, missing-key behavior, and locale-loading approach as part of the early architecture/design-system work.
- Support Arabic as a true right-to-left layout, not only translated strings: use direction-aware/logical layout properties, verify navigation and side panels, form alignment, text inputs, menus, dialogs, focus/navigation order, and direction-sensitive icons. Preserve sensible display of mixed Arabic/English text, identifiers, code, URLs, paths, provider/model names, and numeric data.
- Use locale-aware formatting for dates, numbers, and other formatted values where appropriate, without changing canonical identifiers, stored data, protocol values, or backend semantics.
- Persist the user's language preference across application restarts through an explicitly owned host/application preference mechanism. Slice 1 must identify the appropriate persistence owner; do not place secrets or other sensitive information in browser storage, and do not invent a new general settings subsystem solely for locale.
- Define a localization path for structured Management/application errors: map known stable error codes to localized UI messages where available; provide a generic localized fallback for unknown errors, and keep technical diagnostics separate and safely redacted. Do not blindly translate raw exception text or move business/error policy into React.
- Verify Arabic font fallback/glyph coverage, baseline/line-height, clipping, and readability at supported DPI and window sizes.
- Establish frontend tests for language switching, document `lang`/`dir`, translation-resource completeness, structured-error localization/fallback, Arabic font rendering, and both LTR/RTL layouts. The parity register must distinguish untranslated/missing resources from missing feature behavior.

The localization framework and cross-boundary contract are part of the early build; completing and reviewing all Arabic translations may be done incrementally after the initial English experience is functional.

## 4. Migration boundary

### 4.1 In scope for current-state parity

Port behavior that is actually implemented today in Hive-owned screens, including applicable existing behavior for:

- Workspace shell and module navigation, including modeless single-instance launch/reuse and the supported embeddable Workspace surface;
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

The inventory must be reconciled against actual screens, call sites, and existing automated/manual acceptance paths before each corresponding area is considered migrated. The list above is a checklist to investigate, not a claim that every behavior is implemented identically or can be ported without changes. The baseline is the current Hive-owned screens/behaviors delivered through the existing host projects; it does not mean replacing the Example Host's example-navigation/test tools with React.

### 4.2 Explicitly preserve outside the replacement UI

- All existing C# domain, Management, provider, agent, coordination, execution, persistence, authorization, validation, and durable-state behavior unless a separate authorized requirement explicitly changes it.
- Existing public contracts and compatibility semantics.
- WinForms host integration, bounded host-context discovery, business-app control/data-surface adapters, and reusable host-facing types still used by integrated WinForms applications.
- `Hive.Example.WinForms` as an independent consumer/example/verification host with its established navigation and exact example/test handoff conventions; preserve it as a working consumer and do not convert it into the platform or the React app.
- SQL Server and Embedded persistence parity and the existing secret-protection boundaries.
- The distinction between Hive resources and the user's host-application business data.

### 4.3 Out of scope

- Rewriting Hive.Core, Hive.Management, Persistence, provider transport, agent orchestration, business rules, or authorization in JavaScript.
- Moving database access, provider credentials, or secrets into browser code or browser storage.
- Rewriting or deleting the existing WinForms application/projects in place. The React frontend and its WebView2 desktop host are added as a separate application path.
- Folding React source into Hive.Host.WinForms.UI or making the React frontend depend on legacy WinForms presentation types.
- Replacing or weakening the user's actual WinForms business-app integration UI/contracts; the migration is for Hive's own screens, not for the business applications Hive integrates with.
- Adding new Agent/Manager/Hive modes, orchestration behavior, business-app write capability, or other roadmap features merely because the new UI could display them.
- Treating unstarted Phase 1.20 Slices 3–4 or Phase 1.21 Agent interaction as already implemented. Those features remain under their owning authorized roadmap work; when implemented, their UI can be added through their own acceptance scope.
- Removing or broadly refactoring existing WinForms controls, forms, projects, examples, or documentation as a cleanup step. They are preserved for future reuse; any separately authorized fix must state its narrow purpose.

## 5. Proposed implementation sequence — 12 migration slices

These are proposed planning slices only. They are not current Hive phase/slice identifiers and cannot be started by using a continuation command. Before implementation, the proposal must be accepted, assigned the correct roadmap/Active Work authorization, and have affected architecture documents and project ownership boundaries resolved. The current implemented Hive-owned WinForms presentation is the fixed parity baseline; `Hive.Example.WinForms` remains the independent consumer/verification host, not the new product UI. Do not add new UI features to WinForms during migration just to keep both frontends aligned. Necessary corrections to keep the existing solution buildable or address a material security/correctness issue must be separately bounded. Recommended default sequencing: leave unstarted Phase 1.20 Slices 3–4 and Phase 1.21 work outside current-state parity, keep them under their existing phase ownership, and defer their user-facing UI implementation until React/WebView2 becomes primary. Those phases are neither implemented nor closed by this plan; any different ordering must be resolved before freezing the parity baseline.

### Slice 1 — Architecture and integration spike

**Purpose:** Prove that the separate React application path can be built, run, debugged, packaged, and secured inside the existing solution before investing in feature porting, while leaving the WinForms baseline intact.

Scope:

- Create a standalone React + TypeScript frontend project/package with its own package manifest, lockfile, TypeScript configuration, and frontend checks.
- Create a separate .NET 10 Windows executable host project, preferably a WinForms `WinExe` that embeds WebView2 in the public `HiveForm` shell. Do not turn the existing `Hive.Host.WinForms` library into an executable or repurpose `Hive.Example.WinForms`.
- Inspect the current host-composition ownership and choose between reusing `Hive.Host.WinForms` without launching its legacy screens or extracting only the minimum shared composition needed. A project-level reference to `Hive.Host.WinForms.UI` may be required for the native shell; document the real dependency graph rather than claiming there is no reference to the existing UI assembly. Do not duplicate the Management/service graph.
- Host local React assets in WebView2 through a controlled origin.
- Demonstrate one UI-to-C# command and one C#-to-UI event using a versioned message envelope.
- Verify initialization/disposal, failure messaging, fixed-origin/navigation handling, and one asynchronous sample request.
- Prove all existing WinForms projects remain buildable and preserve their current user-visible behavior; launch the existing `Hive.Example.WinForms` host and the new React/WebView2 executable independently. No existing projects or screens are repurposed or removed. If the shared header needs an extension, it must be additive and opt-in, and existing forms must retain the current header behavior by default.
- Confirm the native header route through the public `HiveForm` shell. The internal `HiveWindowHeader` must remain internal; any EN/AR header action must be opt-in for the new host and leave existing WinForms forms unchanged by default.
- Decide and document how the new React/WebView2 Workspace provides the existing embeddable Workspace capability as well as standalone/modeless launch/reuse. A standalone executable alone is not evidence of embedding parity.
- Choose the localization/resource approach and decide where the user's locale preference is persisted. Define the versioned host-to-frontend locale-change message and initial locale bootstrap behavior.
- Prove a small end-to-end EN/AR language switch in the spike, including the correct HTML `lang`/`dir` attributes, a structured-error translation/fallback example, and an RTL sample with Arabic font glyphs, while keeping the app shell/window controls native.
- Resolve how the canonical Standalone Workbench target is reconciled with this proposed React/WebView2 primary application before Slice 2 can be authorized.
- Exercise the proposed Visual Studio development/debug and production asset workflows.
- Record the final architecture choice and revise affected architecture documents under the authorized scope.

Acceptance:

- The existing WinForms libraries remain buildable; `Hive.Example.WinForms` remains buildable and launchable without the frontend development server.
- The new app does not launch legacy Workspace/Settings forms to render its React content; it uses the existing `HiveForm` shell only through its supported public API, with an opt-in EN/AR header action that does not change legacy forms by default.
- The standalone/modeless and embedding paths have an explicit design and a documented parity decision. Slice 1 proves the proposed embedding route through a bounded feasibility spike; Slice 3 owns the complete WebView2-backed embedding implementation and behavior-level parity. Embedding may not be silently dropped.
- Debug and production paths both load bundled/local UI content without relying on remote web assets.
- Messages cross the bridge only through explicitly registered command/event handlers; no arbitrary host invocation is available.
- Invalid messages and navigation fail closed.
- Missing assets or unavailable WebView2 produce a useful failure instead of a blank or hung window.
- The selected approach is documented, including frontend tooling, project ownership, asset paths, exact developer build/test commands, header integration, localization resources, locale preference ownership, the bridge contract, embedding behavior, and architecture/roadmap reconciliation.
- A minimal EN/AR switch changes locale at runtime and sets the correct LTR/RTL document direction; this spike does not claim that all screens are translated.

Stop/go gate: do not proceed to a broad migration if this slice exposes an unacceptable security, packaging, debugging, memory, or maintenance problem. Reassess the approach before further work; do not silently switch frameworks.

### Slice 2 — Shared design system and interaction primitives

**Purpose:** Establish one durable visual language before porting many screens.

Scope:

- Define design tokens for typography, spacing, surfaces, borders, radii, semantic colors, elevation, and motion.
- Implement reusable buttons, text inputs, selectors, cards, tabs, navigation, empty/loading/error states, confirmation/details dialogs, progress and status presentation.
- Define light/dark/system themes and behavior when the theme changes while a view is open.
- Define validation display, keyboard focus, disabled/busy states, responsive panel rules, and accessibility semantics.
- Establish the shared localization provider/hooks and translation-resource conventions; ensure application-facing text uses translation keys rather than hardcoded strings.
- Add the EN/AR toggle to the new application's native header integration and connect it to the host/React locale contract, including persistent preference behavior.
- Make shared components direction-aware. Use logical styling properties rather than assuming left/right, and account for mixed Arabic/English values.

Acceptance:

- Tokens and primitives are shared rather than duplicated per screen.
- Light, dark, and system theme paths are exercised.
- Keyboard focus order and accessible labels/states exist for the initial primitives.
- Changing EN/AR updates all implemented shared UI text at runtime without restarting; the preference survives an application restart.
- Shared components render correctly in both LTR and RTL layouts, including focus, validation, form alignment, and mixed-direction content.
- Missing translation keys are detectable in automated checks and do not silently masquerade as completed translations.
- Components do not become generic boxes around every content element; the approved desktop-dense/card-selective direction remains intact.

### Slice 3 — React Workspace shell and navigation

**Purpose:** Build the Workspace shell in the new React/WebView2 application while preserving the existing WinForms screens and behavior as the legacy baseline and retaining the module/application ownership boundaries. Do not rewrite the legacy screens; any shared header API extension must remain opt-in and backward-compatible.

Scope:

- Introduce compact left navigation/history, central content, and an optional collapsible right context area.
- Preserve the current independent/modeless Workspace launch/reuse behavior and supported embedding scenario. Define a WebView2-backed embeddable Workspace surface behind a bounded host API, or document an explicit owner-approved compatibility disposition before claiming parity.
- Preserve lazy module creation and module navigation semantics, adapting the module boundary only where required by the web presentation architecture.
- Define browser-history/deep-link behavior only where useful and compatible with the desktop app; browser navigation must not become an authority boundary.

Acceptance:

- Standalone/modeless launch and single-instance reuse work as before, and an equivalent WebView2-backed embedding surface is available where the existing supported contract requires it. If any existing consumer cannot be supported, the cutover is blocked until the owner explicitly accepts a documented compatibility exception.
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

**Purpose:** Close gaps outside the first migrated feature areas, reconcile the full parity inventory, and define the cutover evidence without deleting or rewriting the legacy WinForms implementation.

Scope:

- Inventory remaining current Hive-owned screens and user-facing interaction paths.
- Port remaining Settings/CRUD/editor pages and shared dialogs not already covered.
- Complete shared error, confirmation, accessibility, focus, keyboard, resize, DPI, clipboard, and theme handling where applicable.
- Reconcile translation-resource coverage for every migrated screen and complete/review Arabic translations for in-scope user-visible UI, including dialogs, validation, empty/error states, and accessibility labels.
- Decide which native dialogs and Windows-specific interactions should remain native versus become web-rendered based on usability and accessibility requirements.
- Document each legacy WinForms project/screen that remains preserved after cutover, including its build status, current limitations, and any retained host-integration use. Classify it as frozen fallback/reference, shared integration capability, or an explicitly authorized exception; do not propose removal as routine migration cleanup.

Acceptance:

- The inventory is reconciled against actual project/source/call-site evidence; no screen is deemed complete merely because a similarly named React page exists.
- Each current screen has an explicit acceptance path and a named owning frontend feature.
- Each migrated screen's user-visible text is localized for English and Arabic, and LTR/RTL behavior has been reviewed; intentional language-neutral values are documented.
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
- Preserve the old Hive-owned WinForms UI projects and their current implementation after parity. Do not delete or rewrite them as part of this migration. Confirm the React application is the primary launch/distribution path for new Hive UI use, document the WinForms fallback/reference status, and retain only explicitly authorized compatibility/security/build fixes for the legacy path.

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
- Treat the persisted locale as a non-sensitive user-interface preference owned by the explicitly selected host/application settings mechanism. Do not place secrets or credential-bearing connection strings in browser storage, URLs, telemetry, console output, or unredacted messages.
- Keep translation resources bundled with the application and do not load executable code or arbitrary translations from untrusted remote sources.
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
| Existing architecture ownership becomes contradictory | Treat the current WinForms Workbench statement as authoritative until the owner approves the proposed target change; reconcile the canonical architecture and roadmap in Slice 1 before authorizing feature-migration slices. |
| Migration loses WinForms host-integration utility | Inventory consumers and retain host-facing WinForms types/contracts independently from Hive's own screen implementation. |
| Memory/startup regression on modest machines | One bounded WebView by default, lazy modules, bundle analysis, cold-start/idle-memory measurement. |
| Existing future roadmap work is accidentally pulled into migration | Port only implemented behavior; route unimplemented features to their owning phase and authorization. |
| Old and new UIs are maintained indefinitely | Treat existing WinForms behavior as a fixed parity baseline; after the parity gate, route all new Hive-owned UI features to React and keep WinForms on hold, allowing only separately authorized compatibility/security/build corrections. |

## 10. Effort estimate and confidence

Planning estimate for one developer using AI-assisted implementation and developer-run verification:

- **Workspace-only migration:** approximately 4–6 slices, medium-to-high complexity.
- **Full current Hive-owned UI parity:** approximately 10–14 slices; this document proposes 12 slices.
- **Indicative elapsed effort:** about 4–8 weeks for full current-state parity with EN/AR localization, RTL review, WebView2 packaging, and the required embedding decision, assuming regular work, a successful Slice 1 spike, and no major hidden coupling. This is a low-confidence planning range, not a commitment or measured delivery forecast; reassess it after Slice 1 proves the host/composition, embedding, packaging, and memory costs.

The first slice is intentionally a go/no-go gate. After it exposes actual build, host, bridge, packaging, and memory costs, revise the estimate before authorizing broad feature migration if the evidence materially changes the assumptions.

## 11. Decisions required before implementation authorization

The user/repository owner must explicitly approve or revise these choices:

1. Adopt React + TypeScript + WebView2 as a separate frontend project/package and a separate desktop host/application beside the existing WinForms application; do not fold it into the current WinForms UI projects.
2. Confirm the parity boundary against the exact currently implemented WinForms behavior; unimplemented features remain with their owning phases.
3. Approve the proposed 12-slice sequence or revise it after the spike.
4. Approve the bridge/security design, host-composition ownership, frontend/desktop-host project boundaries, localization resource strategy, locale preference ownership, and the native-header reuse decision (including the EN/AR toggle).
5. Confirm the lifecycle policy: preserve current WinForms projects/source and keep them buildable; after verified parity, put the Hive-owned WinForms presentation on hold and route new UI development to React only, with separately authorized compatibility/security/build fixes as the exception.
6. Approve the architecture-alignment decision against the current Standalone Workbench target and the recommended sequencing for unstarted Phase 1.20 Slices 3–4 / Phase 1.21 UI work. Do not close or advance those phases through this plan; reconcile the architecture and roadmap before authorizing Slice 2.
7. Authorize Slice 1 through the repository's normal Active Work and phase/slice workflow.

Until those decisions are made, this document is planning material only. It does not authorize coding, dependency additions, solution/project changes, migration of current screens, removal of WinForms presentation code, roadmap movement, or changes to Active Work.
