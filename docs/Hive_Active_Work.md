# Hive — Active Work

Status: VERIFICATION FAILED / REMEDIATION REQUIRED

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
- developer reported 16 failed tests after the prior compile remediation: one modality-order assertion, two sealed DispatchProxy fixture failures, one outdated optional-credential status assertion, and 12 Management discovery failures caused by the transient discovery probe intentionally having no model identity.

Remediation boundary:
- keep the transient Management discovery probe model-less; do not reintroduce fake model identity into production discovery;
- update only the affected deterministic tests/fixtures and the credential-status expectation within this same Phase 1.16 follow-up.

Latest remediation:
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
