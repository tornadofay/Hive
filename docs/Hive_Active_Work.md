# Hive — Active Work

Status: VERIFICATION PENDING

Authorized task: **Phase 1.16 Follow-Up — Complete Provider Model Metadata Discovery**

Authorized scope:
- extend the established Provider / ProviderAccount / endpoint discovery boundary with a complete normalized provider-reported model profile, including modalities, capabilities, reasoning/thinking information, model-scoped limits, pricing/economic evidence, operational observation metadata, and bounded provider-specific evidence;
- expose the rich discovery through the Advanced Configuration tree with **Overview** and **Model Information** leaves alongside the existing Providers, Accounts / Credentials, and Execution Targets administration pages;
- add structured known-capability configuration using Supported / Unsupported / Unknown states while preserving configured override authority and automatic-target discovery ownership;
- distinguish built-in provider credential requirements as **No credential**, **Optional credential**, or **Required credential**, with optional credentials remaining Secret Store backed;
- add deterministic automated coverage and a matching Example Host scenario for the new public/observable behavior.

Explicit exclusions:
- no durable Model resource;
- no second provider transport implementation for OpenAI-compatible vendors;
- no periodic/background model discovery;
- no automatic probing of every model;
- no duplication of rich discovery metadata into durable ExecutionTarget configuration;
- no Agent-selection or Workspace redesign;
- no unrelated provider/settings refactor.

Latest verification failure:
- developer reported four remaining failed tests: one thinking-option ordering assertion, one Model Information UI rendering assertion with an empty details box, one stale-refresh assertion that expected only the no-qualifying-target failure, and one stale-refresh input-routing assertion with no prepared image.

Remediation boundary:
- preserve deterministic thinking-option normalization without imposing an inappropriate lexical order on semantically ordered thinking levels;
- ensure the Model Information page renders the first discovered model's details when it auto-selects the first model, even when a test/control handle has not raised the selection event;
- keep the transient Management discovery probe model-less and make the shared deterministic discovery fixture provide a model identity that matches the test target without reading the probe's model field;
- update only the affected deterministic tests/fixture expectations and same-slice UI behavior within this Phase 1.16 follow-up.

Latest remediation:
- updated the rich metadata test to assert the adapter's deterministic case-insensitive modality ordering;
- removed `sealed` from the two Phase 1.16 test `DispatchProxy` fixtures;
- updated the Provider Settings test to exercise the actual Ollama **Optional credential** semantics rather than the obsolete No-credential expectation;
- changed the shared Management `RecordingDiscovery` fixture to use its own deterministic discovered model identity, preserving the production rule that the transient discovery probe has no model identity;
- removed `sealed` from the Model Information Example Host `DispatchProxy` fixture so the manually required example path can instantiate its proxy;
- revision inspection confirmed the affected test and example proxies are proxy-generation compatible and no same-slice residual pattern was found.

Historical remediation:
- guarded nullable `ExtensionData` access before indexing and used the proven null-forgiving boundary after the assertion;
- removed the invalid descriptor-level `IsStale` assertion because freshness belongs to normalized provider metadata/snapshot contracts, not `OpenAICompatibleModelDescriptor`;
- changed filtered `Assert.Single(result.Where(...))` to xUnit's predicate overload.
- corrected the three nullable `SystemFonts.MessageBoxFont` constructor arguments by using the repository's existing non-null font fallback pattern;
- replaced the incompatible `Form` versus `HiveModelInformationSettingsView` conditional expression with an explicit nullable `IWin32Window` owner variable and null-coalescing assignment;
- revision inspection confirmed the affected files contain no remaining direct `new Font(SystemFonts.MessageBoxFont, ...)` calls and no remaining mixed-form/control owner conditional.

Verification boundary:
- developer reported four additional same-slice compiler errors: three nullable SystemFonts.MessageBoxFont prototype arguments and one conditional-expression type mismatch between Form and HiveModelInformationSettingsView;
- required handoff: `Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms`;
- required tests: focused Phase 1.16 discovery/model-information tests plus the broader-suite result supplied by the developer.

Do not start later roadmap work or broaden this slice without an explicit new authorization.
