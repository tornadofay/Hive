# Hive — Active Work

Status: IMPLEMENTATION IN PROGRESS

Latest UI correction:
- corrected the capability editor table header placement so Capability, Set state, and Current occupy row 0 and the first capability (Text generation) starts on row 1; this removes the observed header/capability overlap.
- added regression coverage asserting the three headers remain on row 0 and the Text generation label/selector remain paired on row 1 in columns 0/1.
- no capability semantics, discovery ownership, or execution-target behavior changed.

Verification boundary:
- developer must rerun the focused Execution Target / capability UI tests and the full Hive.Tests suite;
- manually inspect the Execution Target dialog and confirm the capability section reads as aligned Capability | Set state | Current, with each capability on its own row;
- required handoff remains: Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms.


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

Latest verification finding:
- developer clarified that the reported transport exception occurred while **Ollama was not configured**, so the prior diagnosis of a defective deterministic loopback Example Host transport fixture was incorrect; no production transport fault was established by that exception.

Remediation boundary:
- revert only the immediately preceding deterministic Example Host HTTP-reader change because it was based on the incorrect diagnosis;
- separately correct the established OpenRouter model-information integration gap within the current Provider Model Metadata Discovery boundary: parse OpenRouter's documented `/api/v1/models` model metadata, including nested architecture modalities and supported parameters, into the existing normalized discovery contract;
- do not add another transport, probe individual OpenRouter models, or broaden beyond provider-reported model metadata.

Latest verification finding:
- OpenRouter models are returning with no normalized capabilities in Hive even though OpenRouter's documented Models API exposes `architecture.input_modalities`, `architecture.output_modalities`, `supported_parameters`, context limits, pricing, name, and description.

Remediation boundary:
- update the existing OpenAI-compatible adapter's OpenRouter-compatible parsing so these provider-reported fields populate the existing normalized model profile and known capability states;
- retain Unknown when OpenRouter does not report a particular capability; do not infer capabilities merely from model names or descriptions;
- add deterministic adapter regression coverage using the documented OpenRouter response shape.

Latest remediation:
- restored `src/Hive.Example.WinForms/ProviderCapabilityDiscoveryExampleView.cs` to the pre-misdiagnosis fixture implementation because the reported transport exception was caused by an unconfigured Ollama provider, not by that fixture;
- extended the existing OpenAI-compatible adapter to parse OpenRouter's documented general Models API shape: nested `architecture.input_modalities` / `output_modalities` and `supported_parameters`;
- normalized OpenRouter provider-reported evidence into the existing Hive capabilities without model-name inference: image input -> `vision`, text output -> `text.generate`, `tools` -> `tool.calling`, `structured_outputs` -> `structured.output`, and `reasoning` -> `reasoning`;
- reused the existing limits/pricing normalization for OpenRouter's top-level `context_length`, `top_provider.max_completion_tokens`, and `pricing.prompt/completion` fields;
- added deterministic regression coverage for the documented OpenRouter response shape and preserved the raw `architecture` / `supported_parameters` fields as bounded extension evidence;
- revision inspection confirmed production changes are confined to the existing OpenAI-compatible model metadata parser and focused Phase 1.16 test coverage.

Verification boundary:
- developer must rerun the focused Phase 1.16 discovery/model-information tests and the broader `Hive.Tests` suite;
- rerun the affected Provider / Model Capability Discovery Example Host scenario only as a capability-discovery regression check;
- verify the real configured OpenRouter account in Model Information and confirm its reported modalities/capabilities are now populated.

Latest authorization:
- developer authorized expanding the current Phase 1.16 Follow-Up work from the OpenRouter-specific remediation to a bounded provider-by-provider metadata audit and implementation for every built-in provider;
- the expanded goal is to map authoritative provider-reported model metadata into the existing normalized Hive model profile wherever the provider exposes it, while retaining Unknown for fields not reported and keeping a single OpenAI-compatible inference transport;
- no static model-capability catalog, model-name inference, periodic probing, or second inference transport is authorized.

Expanded implementation boundary:
- built-in providers currently cataloged as OpenAI, Groq, OpenRouter, Cerebras, NVIDIA, Google Gemini, Ollama, LM Studio, and Cloudflare;
- inspect each provider's current authoritative model-information surface and implement only evidence supported by that surface;
- reuse the existing ProviderModelMetadata / capability normalization contract and bounded redaction rules;
- add deterministic coverage for each provider-specific response shape that is implemented.

Latest remediation:
- completed the authorized built-in-provider model metadata audit over the existing shared OpenAI-compatible discovery implementation;
- retained the common standard `/models` path for OpenAI, Groq, and NVIDIA, with Groq's provider-reported `active` and `context_window` metadata now normalized without inferring capabilities from documentation or model names;
- added provider-specific authoritative model metadata sources for Cerebras (public OpenRouter-format model catalog), Google Gemini (native Models API), LM Studio (native `/api/v1/models`), Ollama (bulk `/api/tags`), and Cloudflare Workers AI (account model search with marketplace/OpenRouter format);
- mapped provider-reported capabilities, modalities, thinking/reasoning, limits, pricing, availability, descriptions, and other metadata into the existing normalized profile where those fields are actually exposed;
- preserved the single shared HTTP/inference transport implementation; provider-specific work changes model-metadata endpoint selection and response normalization only;
- added deterministic `ProviderModelMetadataProviderTests` coverage for Groq, Cerebras, Gemini, LM Studio, Ollama, Cloudflare, and basic OpenAI/NVIDIA behavior;
- intentionally did not infer capabilities for OpenAI/NVIDIA from model documentation, and intentionally did not call Ollama `/api/show` for every listed model because its current bulk endpoint does not reliably include capabilities and N+1 per-model probing remains outside the authorized boundary;
- revision inspection corrected the earlier misdiagnosed loopback fixture change and found the provider-metadata implementation confined to the OpenAI-compatible provider adapter, capability-discovery routing, focused provider tests, and Active Work documentation.

Verification boundary:
- developer must rerun the focused Phase 1.16 discovery/model-information tests plus the broader `Hive.Tests` suite;
- verify configured OpenRouter, Cerebras, Google Gemini, LM Studio, Ollama, and Cloudflare accounts where available;
- confirm model information displays provider-reported capabilities/metadata and that OpenAI/NVIDIA legitimately show Unknown where their API does not report those fields;
- required handoff remains: `Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms`.

Latest remediation:
- preserved the provider-reported ordering of LM Studio reasoning options instead of passing those semantic levels through lexical modality normalization.
- revision inspection confirmed this correction remains confined to the provider metadata parser and does not alter inference request behavior.

Verification boundary:
- developer must rerun the focused Phase 1.16 discovery/model-information tests and the full `Hive.Tests` suite;
- manually inspect configured provider Model Information for OpenRouter, Cerebras, Google Gemini, LM Studio, Ollama, Cloudflare, Groq, OpenAI, and NVIDIA where configurations are available;
- confirm Ollama bulk discovery makes no per-model `/api/show` calls and that providers without capability evidence remain Unknown rather than inferred.

Latest verification failure:
- developer reported two same-slice compiler errors in `OpenAICompatibleProviderAdapter.cs`: CS1503 from passing the `ModelSerializationFailure()` result into `Result<OpenAICompatibleModelCatalog>.Failure`, and CS1739 from using lowercase named arguments that do not match the `OpenAICompatibleModelDescriptor` constructor parameter names.

Remediation boundary:
- correct only those two compile errors in the provider-specific model metadata implementation; do not alter provider routing or metadata semantics.

Latest remediation:
- corrected the two developer-reported compiler errors in the provider-specific model metadata parser: the decoder-fallback branch now returns the existing model-catalog serialization failure directly, and the model descriptor named arguments now match the constructor parameter names;
- source-level inspection found no additional lowercase named arguments against the `OpenAICompatibleModelDescriptor` constructor in the newly added provider parsers.

Verification boundary:
- developer must rerun the focused Phase 1.16 discovery/model-information tests and the full `Hive.Tests` suite;
- recheck the built-in provider Model Information flows after compilation, including the provider-specific endpoint routing introduced in this slice.


Latest verification failure:
- developer reported same-slice compiler error CS1061 in `tests/Hive.Tests/ProviderModelMetadataProviderTests.cs`: the focused provider metadata tests reference `ProviderModelMetadata.Capabilities`, but the normalized contract exposes this collection as `DiscoveredCapabilities`; the error occurs at 15 test references.

Remediation boundary:
- correct only the focused provider metadata test references to the existing `ProviderModelMetadata.DiscoveredCapabilities` contract;
- do not add a duplicate `Capabilities` production property or alter provider metadata semantics, normalization, or routing.


Latest remediation:
- corrected the 15 focused provider metadata test references from ProviderModelMetadata.Capabilities to ProviderModelMetadata.DiscoveredCapabilities, matching the existing normalized contract;
- left production metadata types and provider discovery behavior unchanged;
- source inspection confirmed no remaining .Capabilities references in ProviderModelMetadataProviderTests.cs.

Verification boundary:
- developer must rerun the focused Phase 1.16 discovery/model-information tests and the full Hive.Tests suite;
- recheck the built-in provider Model Information flows after the test project compiles.


Latest verification failure:
- developer reported 12 same-slice compiler errors in `tests/Hive.Tests/ProviderModelMetadataProviderTests.cs`: the focused tests reference `CapabilityStateEntry.Key`, but the existing contract exposes the capability key as `CapabilityStateEntry.Capability` (a `CapabilityKey`).

Remediation boundary:
- correct only the 12 focused test predicates to use the existing `CapabilityStateEntry.Capability` contract with the repository's existing `CapabilityKey` comparison pattern;
- do not add a duplicate `Key` production property or alter capability contracts/metadata semantics.


Latest remediation:
- corrected all 12 focused provider metadata test predicates from the nonexistent `CapabilityStateEntry.Key` property to the existing `CapabilityStateEntry.Capability` property using the repository's established `new CapabilityKey(...)` comparison pattern;
- left the production capability contract unchanged;
- source inspection confirmed no remaining `capability.Key` predicates in `ProviderModelMetadataProviderTests.cs`.

Verification boundary:
- developer must rerun the focused Phase 1.16 discovery/model-information tests and the full `Hive.Tests` suite;
- recheck the built-in provider Model Information flows after the test project compiles.


Latest verification failure:
- developer reported 492 tests with 9 failures in the Phase 1.16 provider metadata verification.
- eight `ProviderModelMetadataProviderTests` failures return "The provider returned a malformed or unsupported model catalog." for Groq, Cerebras, Gemini, LM Studio, Ollama, Cloudflare, OpenAI, and NVIDIA; source inspection shows the corresponding deterministic JSON test fixtures omit the closing array/object delimiters.
- the existing OpenRouter regression separately fails because `top_provider.max_completion_tokens` is provider-reported model limit evidence but the shared `ParseLimits` normalization does not currently inspect the nested `top_provider` object.

Remediation boundary:
- correct only the malformed deterministic JSON fixtures in `ProviderModelMetadataProviderTests.cs`;
- extend the existing shared model-limit parser to read provider-reported `top_provider` limit fields without changing the normalized contract or adding provider-name inference;
- retain all existing provider routing, capability semantics, bounds, and single-transport behavior.


Latest remediation:
- repaired the seven malformed deterministic JSON response fixtures in `ProviderModelMetadataProviderTests.cs` by restoring their missing closing object/array delimiters; the production catalog parser correctly remains strict about malformed JSON.
- extended the existing shared `ParseLimits` normalization to inspect provider-reported nested `top_provider` limit fields, including `max_completion_tokens`, so the existing OpenRouter metadata regression now maps the documented nested output limit into Hive's normalized model limits;
- preserved the existing bounds, provider-neutral parsing approach, capability semantics, routing, and single HTTP transport.

Verification boundary:
- developer must rerun the focused Phase 1.16 provider metadata tests and the full `Hive.Tests` suite;
- recheck the built-in provider Model Information flows after the tests pass.


Latest verification failure:
- developer reported seven CS8999 compiler errors in `tests/Hive.Tests/ProviderModelMetadataProviderTests.cs`; the repaired raw JSON fixture root-closing `}` lines were inserted without the 12-space indentation required by the surrounding C# raw string literal.


Latest remediation:
- corrected the seven affected JSON root-closing lines in `ProviderModelMetadataProviderTests.cs` to use the 12-space indentation required by the surrounding C# raw string literals;
- source inspection confirmed all seven affected lines now match the raw-string closing indentation.


Latest verification failure:
- developer reran the full `Hive.Tests` suite: 492 tests, 487 passed, 5 failed.
- the remaining four provider-specific catalog failures (Cloudflare, LM Studio, Gemini, Ollama) were routed through the standard `/v1/models` path because the deterministic test provider resource key is constructed as `provider-{providerKey}`, while provider-specific routing intentionally matches the built-in provider keys themselves.
- the Cerebras test consequently received the standard `/v1/models` path instead of the already implemented public `/public/v1/models?format=openrouter` path.
- current official Cerebras documentation confirms the public model metadata endpoint is `GET /public/v1/models` with an optional `format=openrouter`; Cloudflare's model search returns its marketplace data in the `result` array when `format=openrouter` is requested. LM Studio's current native list endpoint is `GET /api/v1/models`, and Gemini's native list endpoint is `GET /v1beta/models`. citeturn615836search1turn503108search0turn418391search0turn418391search8

Remediation boundary:
- correct only the deterministic provider metadata test fixture so the Provider resource uses the same provider key that the production routing contract expects;
- do not weaken provider routing, add aliases for test-only keys, or alter provider metadata parsing because of the fixture mismatch;
- preserve the existing provider-specific endpoint selections and normalized metadata semantics.


Latest remediation:
- corrected the deterministic provider metadata test fixture so the Provider resource key is exactly the built-in `providerKey`; provider-specific routing therefore exercises the intended Cerebras, Gemini, LM Studio, Ollama, and Cloudflare model-information paths;
- left production provider routing and parser behavior unchanged;
- source inspection confirmed the helper now passes `providerKey` consistently as the Provider key and display name.

Verification boundary:
- developer must rerun the focused Phase 1.16 provider metadata tests and the full `Hive.Tests` suite;
- recheck the built-in provider Model Information flows after the provider-specific routing tests pass.


Latest verification failure:
- developer reran the full `Hive.Tests` suite: 492 tests, 491 passed, 1 failed.
- the sole failure is `GeminiDiscovery_UsesNativeModelsApiAndMapsThinkingAndLimits`: the expected Gemini model-list path is `/v1beta/models`, but `BuildGeminiModelsUri` constructs an invalid URI whose parsed absolute path begins with `//generativelanguage.googleapis.com/v1beta...`.
- the defect is isolated to Gemini model-catalog URI construction; provider metadata parsing and the other provider discovery tests now pass.

Remediation boundary:
- correct only `BuildGeminiModelsUri` so it constructs the native Gemini model-list URI from the endpoint's authority and normalized path without introducing an extra leading slash;
- preserve the existing provider-specific route selection and single transport.


Latest remediation:
- corrected `BuildGeminiModelsUri` to construct the Gemini native model-list URI with `UriBuilder`, using the endpoint's scheme, host, port, and normalized model path;
- this removes the extra authority/path slash that produced an absolute path beginning with `//generativelanguage.googleapis.com/...`;
- provider selection, authentication, parsing, and transport behavior remain unchanged.


Authorized UI polish extension within Phase 1.16 Follow-Up:
- rename the visible **Advanced Configuration** surface to **Advanced Provider Configuration** and widen the left navigation tree to fit its existing labels;
- make Execution Targets prefer the Provider Account keyed `default`, falling back to the first available account when a provider is selected;
- redesign Add/Edit Execution Target so Provider and Account are compact read-only context in one row, Model is one editable/selectable ComboBox with no separate Select Model or Refresh actions, Add pre-populates a known built-in provider endpoint, capabilities use a more compact organized layout, and connection-test status shares the footer action bar with Test / Cancel / Save;
- preserve existing Management ownership, discovery semantics, manual/automatic target authority, endpoint validation, cancellation, theming, accessibility, and CRUD behavior.

Implementation checkpoint:
- the requested UI polish has been implemented across the Advanced Provider Configuration form, Execution Targets view/editor, model discovery panel, and capability editor;
- focused regression coverage was updated/added for the renamed surface, navigation width, default-account selection, editable model selection, Add endpoint prefill, custom-model capability reset, and footer test-status placement;
- current UI guidance and user-facing provider configuration strings were aligned with the new Advanced Provider Configuration name.

UI verification boundary:
- developer must rerun the focused UI/discovery tests and the full `Hive.Tests` suite;
- manually exercise `Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms`, including Advanced Provider Configuration and the Execution Targets page/dialog in the updated layout.

Latest developer verification:
- developer reran the full `Hive.Tests` suite after the UI polish implementation: **492 Tests (492 Passed, 0 Failed, 0 Skipped)** in approximately 1.2 minutes;
- this result predates the final focused-test attribute correction recorded below, so it is not treated as the final post-correction verification result.

Latest remediation:
- source audit found a duplicated `[Fact]` on `Editor_AutomaticTargetClearsStaleDiscoveredCapabilitiesWhenModelBecomesCustom` and a missing `[Fact]` on `Editor_ConnectionTestStatusLivesInFooterActionBar`;
- corrected those test attributes only, preserving the production UI implementation and test semantics.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full `Hive.Tests` suite after the test-coverage correction;
- final manual verification remains required for `Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms`, including Advanced Provider Configuration and the updated Execution Targets page/dialog layout.


Latest remediation:
- corrected the focused footer-status UI test to locate the existing `HiveEditorLayout` through the editor's public WinForms control tree instead of accessing protected `HiveForm.BodyPanel`;
- no production UI visibility or architecture was changed.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full `Hive.Tests` suite;
- the requested next UI polish remains blocked until that rerun clears this verification gate.


Latest verification failure:
- developer reported compile error CS0103 in `Hive.Tests/HiveExecutionTargetDiscoverySettingsTests.cs`: the footer-status regression calls `FindControl<HiveEditorLayout>`, but that helper is not defined in this test class.

Remediation boundary:
- correct only the focused test to use an available local control-tree traversal or direct child lookup;
- do not alter production UI visibility or add a production testing-only API.


Latest remediation:
- added the missing focused-test `FindControl<TControl>` helper used by the footer-status regression;
- the helper is test-local and does not change production UI contracts.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full `Hive.Tests` suite before further Phase 1.16 UI polish changes.


Latest verification failure:
- developer reran `Hive.Tests`: **496 Tests (482 Passed, 14 Failed, 0 Skipped)**.
- `Phase116FollowUpTests.AdvancedConfigurationTree_UsesOverviewAndModelInformationLeaves` expects navigation splitter distance 280 but the instantiated form reports 124, so the current test assertion does not match the effective WinForms layout.
- multiple new `HiveExecutionTargetDiscoverySettingsTests` and the related Provider Settings editor test fail before exercising behavior because WinForms `ComboBox.AutoCompleteMode` requires an STA thread.
- `ExecutionTargetsView_SelectingProviderSelectsDefaultAccount` fails because its `DispatchProxy` base test proxy is sealed.

Remediation boundary:
- correct the navigation-width assertion/implementation boundary so the left tree is genuinely wide enough for the requested labels and the test measures the intended width;
- make the affected WinForms-focused tests run under the repository's existing STA test mechanism or add the minimal test-only STA fixture mechanism required by the current test infrastructure;
- unseal only the failing test proxy type required by `DispatchProxy`;
- do not weaken production WinForms or HiveForm visibility contracts and do not alter unrelated behavior.


Latest remediation:
- added `Xunit.StaFact 1.2.69` test support compatible with the repository's xUnit v2 test project and marked the WinForms-focused tests with `WinFormsFact` so WinForms controls run with the required STA context;
- unsealed the focused `ExecutionTargetsManagementProxy` test proxy so `DispatchProxy` can generate its proxy type;
- widened the Advanced Provider Configuration navigation pane to a 320px splitter target with explicit panel minimums and applied the splitter distance after the initial layout pass;
- aligned the Advanced Configuration navigation regression assertion with the new 320px width.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full `Hive.Tests` suite;
- the next requested capability-layout polish remains blocked until this rerun passes.

Latest verification failure:
- developer reran Hive.Tests: 496 Tests (492 Passed, 4 Failed, 0 Skipped).
- HiveExecutionTargetDiscoverySettingsTests.Editor_AutomaticTargetClearsStaleDiscoveredCapabilitiesWhenModelBecomesCustom reports two capability entries after switching to a custom model where the regression expects no stale configured capabilities.
- HiveExecutionTargetDiscoverySettingsTests.DiscoveryPanel_UnsupportedEnumerationLeavesManualEntryAvailable reports the model selector disabled when manual entry is expected to remain enabled.
- HiveExecutionTargetDiscoverySettingsTests.DiscoveryPanel_FailureLeavesManualEntryAvailable reports the model selector disabled when manual entry is expected to remain enabled.
- Phase116FollowUpTests.AdvancedConfigurationTree_UsesOverviewAndModelInformationLeaves throws InvalidOperationException during form construction because the requested 320px splitter distance exceeds the effective available width once the panel minimum sizes are applied.

Remediation boundary:
- correct only the stale automatic-target capability clearing behavior exercised by the failing execution-target regression;
- preserve manual model entry when discovery is unsupported or fails, without weakening discovery error handling or cancellation semantics;
- make the Advanced Provider Configuration navigation width deterministic within the form minimum-size/layout constraints so the tree is genuinely wider without violating SplitContainer bounds;
- do not alter unrelated provider metadata behavior or begin the requested capability-layout polish until this verification boundary is cleared.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite;
- the requested capability-layout polish remains blocked until this rerun passes;
- required Example Host handoff remains: Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms.


Latest remediation:
- corrected the discovery model ComboBox text-change boundary so entering a custom model identifier clears the stale discovered selection instead of allowing the previous discovered item to remain selected;
- aligned the two discovery tests with the documented/manual-entry behavior: provider discovery failure and unsupported enumeration keep manual model entry enabled;
- reduced the Advanced Provider Configuration content minimum from 520px to 300px while retaining a 320px navigation splitter target, making the requested wider navigation feasible at the form's effective width without violating SplitContainer bounds;
- revision inspection compared the remediation with the recorded four-failure checkpoint and found only the focused discovery selection behavior, navigation sizing, and corresponding test assertions changed.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite;
- the requested Management-before-Capabilities reorder and simplified capability editor layout remain blocked until this rerun passes;
- required Example Host handoff remains: Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms.

Latest verification failure:
- developer reran Hive.Tests: 496 Tests (493 Passed, 3 Failed, 0 Skipped).
- HiveExecutionTargetDiscoverySettingsTests.Editor_AutomaticTargetClearsStaleDiscoveredCapabilitiesWhenModelBecomesCustom still retains two discovered capability entries after custom model text is entered.
- HiveExecutionTargetDiscoverySettingsTests.Editor_AddPrefillsBuiltInProviderEndpointAndUsesEditableModelCombo now loses the typed custom model text and reports an empty ComboBox text.
- Phase116FollowUpTests.AdvancedConfigurationTree_UsesOverviewAndModelInformationLeaves still throws InvalidOperationException while constructing the Advanced Provider Configuration form because SplitterDistance is assigned before SplitContainer has a valid laid-out width.

Remediation boundary:
- fix the custom-model text/selection synchronization without clearing the user's typed text, while ensuring automatic-target discovered capabilities are cleared when the selected model becomes custom;
- defer Advanced Provider Configuration splitter-distance assignment until the split container has a valid size, while preserving the requested 320px navigation width and feasible content minimum;
- do not change unrelated discovery, capability semantics, or begin the requested capability-layout polish until this verification boundary is cleared.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite.


Latest remediation:
- removed the attempt to clear ComboBox SelectedIndex from the model TextChanged handler, which was mutating the user's custom model text;
- changed the execution-target editor's text synchronization to treat any text that does not exactly match the currently selected discovered model as custom input and clear discovery-owned capability state through the existing capability editor boundary;
- removed the premature Advanced Provider Configuration SplitterDistance assignment from the SplitContainer initializer and retained the 320px assignment only after the containing layout has been established;
- revision inspection confirmed the remediation is confined to the two model-selection UI handlers, Advanced Provider Configuration splitter construction, and the existing same-slice Active Work record.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite;
- the requested Management-before-Capabilities reorder and simplified capability editor layout remain blocked until this rerun passes;

Latest remediation:
- made the automatic execution-target custom-model reset explicit in HiveExecutionTargetEditorForm: when the model text no longer matches the selected discovered model, Automatic mode reconfigures the capability editor with an empty discovery-owned capability set instead of relying on an indirect SetDiscovery side effect; Manual mode continues to preserve configured overrides while clearing discovery evidence;
- removed Panel1MinSize and Panel2MinSize assignments from Advanced Provider Configuration construction so WinForms cannot reject them against the not-yet-laid-out SplitContainer width; the fixed 320px navigation splitter is now assigned only after the containing controls have completed their initial layout;
- revision inspection confirmed the remediation is confined to custom-model capability synchronization and Advanced Provider Configuration navigation sizing.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite;
- the requested Management-before-Capabilities reorder and simplified capability editor layout remain blocked until this rerun passes.

Latest verification failure:
- developer reran Hive.Tests: 496 Tests (495 Passed, 1 Failed, 0 Skipped).
- HiveExecutionTargetDiscoverySettingsTests.Editor_AutomaticTargetClearsStaleDiscoveredCapabilitiesWhenModelBecomesCustom still retains two discovered capability entries after custom model text is entered; the remaining issue is the ordering of ComboBox selection/text notifications around the custom-model transition.

Remediation boundary:
- guard the execution-target editor from applying a discovered-model capability set when the currently visible model text no longer matches that discovered model;
- keep custom model input authoritative for the automatic-target capability reset without changing provider discovery or capability semantics;
- do not broaden scope beyond this final same-slice failure.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite.

Latest remediation:
- guarded HiveExecutionTargetEditorForm.ModelSelected synchronization so a discovered model's capability evidence is applied only when its model id still exactly matches the ComboBox's visible text; stale selection notifications during custom-model entry are therefore ignored;
- preserved the explicit automatic-target reset for custom model text and all existing discovery/capability semantics;
- revision inspection confirmed the production change is isolated to the execution-target model-selection synchronization boundary.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite;
- the requested Management-before-Capabilities reorder and simplified capability editor layout remain blocked until this verification passes.Latest verification failure:
- developer reran Hive.Tests: 496 Tests (495 Passed, 1 Failed, 0 Skipped).
- HiveExecutionTargetDiscoverySettingsTests.Editor_AutomaticTargetClearsStaleDiscoveredCapabilitiesWhenModelBecomesCustom still retains the discovered capability set [vision=Supported, tool.calling=Supported] after custom model text is entered.
- The prior editor-level model-id guard did not fully prevent a later stale ComboBox selection notification from reapplying discovered capability evidence.

Remediation boundary:
- make custom-model state authoritative inside HiveProviderModelDiscoveryPanel so once visible text diverges from the selected discovered model, later selection notifications cannot reassert the stale discovered model;
- preserve editable custom model text and existing discovered-model behavior;
- do not alter provider discovery transport, capability semantics, or unrelated UI.

Latest remediation:
- made custom model text authoritative inside HiveProviderModelDiscoveryPanel with an explicit custom-entry state;
- selection-change processing now ignores stale ComboBox index notifications while custom entry is active, preventing discovered capabilities from being re-applied after custom text is entered;
- explicit user selection from the discovered-model list clears custom-entry state and reapplies the selected model through the existing ModelSelected path;
- disposed the new selection-commit event handler with the control;
- revision inspection confirmed the change is confined to the model selector event state machine.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite;
- the requested Management-before-Capabilities reorder and simplified capability editor layout remain blocked until this verification passes.

Latest verification finding:
- developer reran Hive.Tests: 496 Tests (495 Passed, 1 Failed, 0 Skipped).
- the sole failure occurs at the first assertion in HiveExecutionTargetDiscoverySettingsTests.Editor_AutomaticTargetClearsStaleDiscoveredCapabilitiesWhenModelBecomesCustom, before custom model text is entered;
- CreateSnapshot supplies two discovered capabilities (vision=Supported and tool.calling=Supported), and Automatic mode intentionally loads the discovered capability set, so Assert.Single is inconsistent with the test fixture and current Automatic semantics.

Remediation boundary:
- correct only the focused regression assertion so it verifies the intended Automatic discovery state before testing the custom-model transition;
- do not alter production capability or model-selection semantics.

Latest remediation:
- corrected the sole failing regression assertion: the Automatic target test now explicitly verifies the two provider-discovered capabilities from its fixture (vision=Supported and tool.calling=Supported) before entering custom model text;
- the existing final assertion that custom model entry clears the automatic discovery-owned capability set remains unchanged;
- revision inspection confirms no production code was changed for this failure; the remediation is limited to the focused test assertion and Active Work record.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite;
- the requested Management-before-Capabilities reorder and simplified capability editor layout remain blocked until this verification passes.


Latest developer verification:
- developer reran the full Hive.Tests suite after the final Phase 1.16 remediation: 496 Tests (496 Passed, 0 Failed, 0 Skipped) in 54.4 seconds.
- this clears the verification gate for the already-authorized Execution Targets UI polish within the current Phase 1.16 Follow-Up slice.

Current UI polish implementation scope:
- move Management before Capabilities in the Execution Target editor;
- simplify the structured capability editor so every known capability uses one consistent state-selector column, with compact current/evidence presentation instead of the four-column Discovered / Override / Effective grid;
- preserve Manual/Automatic authority, configured overrides, discovered evidence, Unknown semantics, accessibility, theming, and existing CRUD behavior;
- add focused UI regression coverage for field order and consistent capability-selector layout;
- update the owning Phase 1.16 UI guidance to describe the implemented first-look capability presentation.

Verification boundary after implementation:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite;
- required Example Host handoff remains: Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms.

Latest implementation:
- developer verification cleared the prior remediation gate with Hive.Tests 496/496 passed.
- moved Management before Capabilities in the Execution Target editor so ownership mode is established before capability configuration;
- replaced the four-column capability presentation with a simpler three-column Capability / Set state / Current layout where every capability uses the same state-selector column;
- Automatic targets show a disabled Managed by discovery selector and the effective Current state; Manual targets can choose Supported / Unsupported / Unknown or Not configured, with Current indicating whether the effective state comes from an override, discovery, or is not reported;
- preserved configured override authority, automatic discovery ownership, unknown/unreported semantics, additional provider-specific evidence, accessibility, theming, and existing CRUD behavior;
- added focused regression coverage for Management-before-Capabilities ordering and consistent capability-selector placement/source presentation;
- updated the owning UI/Phase 1.16 guidance from planned capability-layout language to the implemented presentation.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite after this UI polish;
- manually exercise the updated Execution Target dialog and Advanced Provider Configuration in the required Example Host scenario;
- required handoff: Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms.

Latest implementation refinement:
- Manual capability selectors now remove the Managed by discovery option entirely; only Automatic targets expose that disabled state.
- focused UI coverage now proves the Manual selector excludes Managed by discovery, while Automatic exposes it disabled and shows the discovered Current state.
- revision inspection found no changes outside the authorized Execution Target capability UI, its focused tests, and the owning documentation.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite after the final UI refinement;
- manually exercise the updated Execution Target dialog and Advanced Provider Configuration in the required Example Host scenario;
- required handoff: Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms.

Latest UI refinement:
- changed the capability state-selector column from percentage sizing to a fixed 190px column so Text generation and every other capability selector occupy the same horizontal position regardless of available dialog width;
- retained the Capability and Current columns around that fixed selector column;
- added focused regression coverage proving the selector column is fixed-width and that Text generation's label and selector occupy the same row;
- no capability authority or discovery semantics changed.

Verification boundary:
- developer must rerun the focused UI/discovery tests and the full Hive.Tests suite;
- manually inspect the Execution Target dialog to confirm the capability rows are visually aligned and the first Text generation selector sits directly in the intended state column;
- required handoff: Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms.
