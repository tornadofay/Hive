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
- developer reported a same-slice runtime exception: `Optional model metadata cannot exceed 256 characters. (Parameter 'description')`.

Remediation boundary:
- retain bounded validation for provider-reported normalized metadata, but do not apply the short identifier/display-field limit to model descriptions;
- change only the affected model-description normalization boundary and supporting same-slice regression coverage/documentation.

Latest remediation:
- added a dedicated 4,096-character bound for normalized provider model descriptions while retaining the existing 256-character bound for shorter optional metadata fields;
- added deterministic regression coverage proving a 1,024-character description is preserved and a 4,097-character description is rejected;
- revision inspection confirmed the change is isolated to the Phase 1.16 Core metadata contract and its focused regression test.

Verification boundary:
- developer must rerun the focused Phase 1.16 discovery/model-information tests and the broader `Hive.Tests` suite;
- required Example Host handoff remains: `Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms`.

Latest remediation:
- extended credential-bearing URI redaction to recognize the underscore-form `api_key` query parameter used by the deterministic rich-metadata fixture;
- moved stale-result rejection to the Management discovery-core boundary so direct discovery still rejects stale fresh results, while the internal capability-routing path may inspect the first stale observation and then issue a forced refresh;
- preserved the intentionally model-less transient discovery probe and the deterministic `vision-model` fixture correlation;
- revision inspection compared the remediation with the preceding verification checkpoint and found only the Active Work record plus the provider adapter and Management stale-routing changes.

Verification boundary:
- developer must rerun the focused Phase 1.16 discovery/model-information tests and the broader `Hive.Tests` suite;
- required Example Host handoff remains: `Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms`.

Latest remediation:
- changed thinking-option normalization to preserve the provider-reported option order while deduplicating case-insensitively and retaining deterministic bounded output; semantic levels such as low / medium / high are therefore not reordered lexically;
- updated Model Information snapshot application to render the first discovered model details explicitly when it auto-selects the first model, so the details surface does not depend on a WinForms selection event being raised;
- updated the stale-refresh test to assert both the no-qualifying-target failure and the surfaced stale provider-discovery failure returned by the documented input-preparation boundary;
- changed the shared Management `RecordingDiscovery` fixture to return the deterministic `vision-model` identity used by the test target without reading the transient probe's model field, preserving capability correlation while keeping the production probe model-less;
- revision inspection compared the remediation against the prior verification checkpoint and found only the Active Work update plus the Model Information, provider adapter, and affected Management test changes.

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

Latest verification failure:
- developer reported a same-slice runtime exception: `hive.provider.openai-compatible.transport-failed: The provider model-discovery request failed at the transport boundary.`.

Remediation boundary:
- trace the provider model-discovery transport path used by the Phase 1.16 verification/example; correct only the deterministic local discovery transport/test fixture or directly implicated same-slice integration boundary so the discovery request completes reliably, without weakening production transport error handling or introducing a second provider transport.

Latest remediation:
- hardened the deterministic loopback Provider / Model Capability Discovery example's HTTP request reader to consume bounded header blocks in chunks rather than allocating and reading one byte at a time;
- treated a client-side connection close before a complete request as a normal abandoned connection instead of faulting the example server task;
- preserved the existing 16 KiB request-header bound, OpenAI-compatible request-path validation, production adapter transport error handling, and single provider transport implementation;
- revision inspection compared the remediation with the recorded transport failure checkpoint and found the code change isolated to the deterministic Example Host fixture.

Verification boundary:
- developer must rerun the focused Phase 1.16 discovery/model-information tests and the broader `Hive.Tests` suite;
- additionally rerun the affected Example Host capability-discovery scenario: `Example to run: Providers / Target Selection / Capability Discovery — Hive.Example.WinForms`;
- the required new-slice handoff remains: `Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms`.
