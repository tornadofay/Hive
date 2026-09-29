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

Verification failure boundary:
- developer reported same-slice compile errors in the Phase 1.16 Follow-Up implementation;
- remediation is limited to correcting those reported compile defects and directly necessary supporting code in the affected files;
- after remediation, Active Work returns to **VERIFICATION PENDING** for developer re-verification.

Reported failures:
- `HiveModelInformationSettingsView.InitializeAsync(CancellationToken)` was not public for interface implementation;
- missing `ApplyTheme`;
- inaccessible `ProviderEndpointIdentity` from the WinForms project;
- duplicate `rateLimit` / `remaining` locals;
- `ICollection<string>.AddRange` usage;
- constant-pattern errors in the endpoint scheme check;
- possible null argument passed to `ComboBox.ObjectCollection.Add`;
- `Form ?? HiveModelInformationSettingsView` incompatible null-coalescing operands;
- unassigned `remaining` flow resulting from the duplicate declaration.

Verification handoff after remediation:
- required Example: `Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms`;
- required tests: focused Phase 1.16 discovery/model-information tests plus the broader-suite result supplied by the developer.

Do not start later roadmap work or broaden this slice without an explicit new authorization.
