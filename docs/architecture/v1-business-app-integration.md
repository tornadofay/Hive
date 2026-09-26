# Hive Architecture — V1 Business-App Integration

This document is part of the authoritative architecture defined by `docs/architecture.md`. It contains the detailed V1 host-adapter, business-data, business-operation, write-receipt, and post-write review contracts.

## 1. Purpose and boundary

V1 automates data entry from supported input sources into an existing business application. The business application's domain model, database, validation rules, transactions, and authoritative records remain owned by that application. Input interpretation and candidate generation are separate from business-operation execution.

Hive therefore needs an integration boundary that can understand enough of a host application to perform a governed operation without becoming coupled to one control library, ORM, database schema, or UI framework.

The business-operation boundary consumes a structured, validated proposal independently of how the candidate was produced. Candidates may originate from vision extraction, spreadsheet mapping, or another supported Hive capability. The host adapter therefore remains source-independent.

The architectural direction is:

```
Hive-owned neutral contracts
          ↓
host/application adapter
          ↓
host-specific controls / data model / API
```

Examples of host implementations may include:

- native WinForms controls;
- application-owned/custom WinForms controls;
- another application-owned/custom control or data framework;
- another developer's control library;
- another host integration implementation added later.

Hive must not make any application-specific control, data, UI, ORM, or business-layer type a platform dependency.

The first concrete host implementation remains the V1 WinForms boundary. Generic cross-host technology support remains Phase 7 work; the neutral contracts in this document exist so the V1 WinForms adapter itself is not vendor/control-library-specific.

## 2. Three distinct integration layers

V1 host integration has three different concerns:

```
1. Host context / discovery
       ↓
2. Host interaction and data surfaces
       ↓
3. Business operations
```

They must not be collapsed.

### 2.1 Host context / discovery

Discovers bounded, immutable information about the registered host context.

Examples:

- form/control hierarchy;
- semantic control metadata;
- binding metadata;
- data-source descriptors;
- available UI capabilities.

Discovery alone grants no mutation authority.

Phase 1.13 already establishes the concrete WinForms discovery boundary.

### 2.2 Host interaction and data surfaces

Provides bounded operations over the host application when an authorized operation genuinely needs the UI or host data surface.

Examples:

- read a control value;
- set a permitted control value;
- select an existing lookup value;
- add/edit/delete a row;
- read a bound collection;
- resolve the stable identity of a row;
- invoke an explicitly exposed host action.

These are capabilities, not authorization.

### 2.3 Business operations

Represents an application-level operation such as:

```
CreateInvoice
UpdateCustomer
CreateOrder
PostDocument
```

A planned business operation may be implemented through an API/service, a UI workflow, or a composition of both. The implementation mechanism must not redefine the business meaning.

## 3. Neutral public extension contracts

Hive should ship public, host-neutral contracts for the concepts required by V1. Contract ownership follows the roadmap phase that introduces each capability; later V1 phases may extend the neutral contract family without changing the host-neutral boundary.

The Phase 1.14 public contract family must support at least:

- a host integration registration/adapter boundary;
- semantic control descriptors;
- data-source/data-surface descriptors;
- field/column descriptors;
- stable row identities;
- lookup descriptors and bounded lookup operations;
- bounded interaction operations;
- business-operation capability boundaries needed for API/UI composition.

Durable business-operation receipts and first-class Review records are owned by Phase 1.18. Their semantics are defined in Sections 12–13 so the earlier host-integration contracts do not have to be redesigned later.

These contracts must not expose:

- WinForms `Control` types;
- concrete control-library interfaces or host-specific control types;
- `DataTable`/ORM-specific types as required public contracts;
- `SqlConnection`, SQL commands, or raw SQL expressions;
- provider credentials;
- arbitrary host object references;
- unrestricted reflection or arbitrary method invocation.

The concrete WinForms adapter may internally use the host APIs required to translate or perform an authorized bounded operation. That does not transfer database, business, or authorization ownership into the adapter.

### 3.1 Contract placement

Pure host-integration semantics belong in `Hive.Core` because they must remain dependency-light, host-neutral, and usable by any host adapter. They may reference other pure Core contracts such as Hive identities and resource references, but they must not reference WinForms, concrete UI controls, SQL providers, ORMs, host business types, or MAF implementation types.

Concrete WinForms adaptation belongs in `Hive.Host.WinForms`. `Hive.Management` owns application-facing orchestration and authorization and consumes the host-integration ports through dependency injection/composition. The ports are defined by the neutral Core contract so the dependency direction remains `Hive.Core → Hive.Management → Hive.Host.WinForms`; Management never references the concrete WinForms adapter. `Hive.Host.WinForms` must not bypass Management for management/application operations.

A host-specific adapter may use live host objects internally when required to perform an authorized operation, but those objects must not escape through the neutral public contract. Database access remains owned by the host application's business/data layer; Hive does not execute host SQL merely because an adapter can identify a table or field.

Do not add a new universal host framework merely to support the first V1 host. Create only the Core-level ports and neutral contracts required by the actual V1 integration boundary. Host implementations are supplied by the application composition root.

## 4. Semantic control model

A host control descriptor should expose semantic information rather than reproduce the host control's entire property bag.

Conceptually:

```
ControlDescriptor
├── identity/path
├── presentation labels
├── current state
├── binding metadata
├── data-source metadata
├── field metadata
└── supported capabilities
```

Potential semantic fields include:

- logical/control identity;
- runtime type name;
- display/title metadata;
- English/Arabic labels when the host exposes them;
- bound field/property name;
- data type and relevant size/precision metadata;
- required/read-only/computed state, including host-required-field validation semantics such as `RequiredField` when exposed;
- primary-key/identity metadata;
- generation semantics;
- lookup metadata;
- current value where reading is allowed.

Not every host property should become Hive context. Designer-only, rendering-only, reporting-only, or implementation-specific properties remain outside the normal agent-facing semantic projection.

### 4.1 Capability state versus authorization

These are independent:

```
Host says:
    edit is supported

does not mean:

Hive says:
    this caller is authorized to edit
```

Likewise:

```
AllowNew = true
AllowEdit = true
AllowDelete = true
```

describe host/application behavior. They do not grant Hive authorization.

Hive authorization remains enforced in code through the existing security/policy boundaries.

### 4.2 Host application permission switches

Application-owned settings such as:

```
AllowPermissionCheck
AllowUserLogHandling
```

may be meaningful host metadata, but they do not replace Hive authorization.

Hive must not infer that a disabled host permission check allows Hive to bypass Hive policy.

## 5. Data surfaces and related data

A grid or bound collection is a data surface, not automatically a database table.

The neutral model is:

```
Host entity/data surface
        ↓
data source / collection
        ↓
rows
        ↓
fields
```

A data surface may represent:

- a `BindingSource`;
- a `DataTable`;
- `IList<T>`;
- another collection;
- an application-owned data object;
- a remote/API-backed data surface exposed by the host.

The contract must not require SQL Server, a particular ORM, or a relational database.

### 5.1 Parent/child data

The V1 contract must represent a related data set explicitly when the host supplies it:

```
Invoice
├── identity
├── fields
└── Lines
     ├── Product
     ├── Quantity
     └── Price
```

The relationship should be expressed semantically:

```
Parent entity
    ↓
child collection
    ↓
parent-key → child-key relationship
```

Do not infer a relationship merely because:

- a grid happens to be visually nested in a form;
- two names look similar;
- a database field is hidden;
- a filter string references another table.

The host adapter may use its own relationship metadata to establish the semantic relationship.

### 5.2 Host-provided parent/child mapping

The host integration contract represents parent/child relationships explicitly when the host application provides them. Hive does not infer these relationships from visual layout, naming, hidden fields, SQL, or control nesting.

Conceptually:

```text
root data surface
      ↓
explicit child-collection metadata
      ↓
child data surface
      ↓
parent identity → child relationship
```

The host application remains responsible for implementing or exposing the authoritative relationship semantics. Hive consumes that information through the neutral contract.

Parent identity propagation, child editing, and combined parent/child save are supported host capabilities when the host implementation provides them. These semantics are represented through the contract; Hive does not copy the host application's data model or persistence implementation.

## 6. Stable row identity

Stable identity is mandatory for consequential row operations.

For the current V1 integration contract, the stable row identity is the record's primary-key **ID**.

Row index is not an authoritative identity.

```
row index = positional address
row identity = primary-key ID
```

An operation that begins from a row index resolves the selected row through the host's existing row/ID behavior before a consequential operation is committed. The current V1 host model normally does not change an existing primary-key ID. A DataGridView editing path may delete and reinsert a saved row internally, but the existing host mechanism already handles that case through row-index-based editing and it is not a separate Hive identity requirement.

The neutral contract may remain extensible to other identity shapes in future hosts, but V1 does not require a composite-key implementation or a general key-mutation protocol.

### 6.1 Hidden primary-key columns

A host may keep its primary-key column invisible to the user:

```
Id            Visible = false
Product       Visible = true
Quantity      Visible = true
Price         Visible = true
```

This is valid and useful.

The semantic signal must be explicit:

```
IsPrimaryKey = true
Visible = false
```

Visibility alone never means "primary key".

The adapter should therefore be able to expose an identity field even when the field is not visible in the UI.

### 6.2 Identity shape outside the V1 host

The current V1 integration contract uses a single primary-key **ID**. Composite or other identity shapes are not required by the current V1 implementation boundary.

The neutral architecture remains extensible so a future host can introduce another stable identity form without redefining the overall host-integration boundary.

### 6.3 Generation semantics

Generated fields are supported by the V1 host contract, but their implementation mechanism is not a contract requirement. Generation may come from the database, host code, computed properties, or another host-specific mechanism.

Hive therefore treats generation semantically:

- whether the field is generated;
- whether the host supplies the resulting value;
- whether the field may be supplied by the caller before the operation;
- what resulting value/identity the host returns after create or save.

Hive must not invent generated values merely because an add-row operation requires the field.

### 6.4 Computed fields

Computed fields are supported by the V1 host contract. Their implementation mechanism is host-defined and may take many forms.

The neutral contract should describe the semantic fact that a field is computed and, separately, whether the host permits direct assignment to it. Hive should not require a particular attribute, property pattern, database expression, or other implementation technique merely to recognize a computed value.

## 7. Lookup columns

A lookup column is a semantic relationship, not an executable SQL expression.

Conceptually:

```text
Lookup
├── identity
├── display value
├── stored value
└── bounded filtering/query capability
```

A host may provide table/entity names, display/value field metadata, or a host-defined filter descriptor. Those details are adapter inputs and must be translated into a bounded lookup contract.

Lookup resolution may depend on:

- values from the current record;
- other host/application context;
- both at the same time.

The neutral contract should therefore allow bounded lookup resolution to receive the relevant host-provided context without exposing arbitrary SQL, unrestricted filtering, or private host objects.

The host adapter owns actual lookup execution and returns bounded options such as:

```text
value = 42
display = "Product A"
```

The model can choose an option; it cannot execute arbitrary lookup SQL or arbitrary host queries.

## 8. UI edit surfaces and operation capabilities

UI configuration may describe how the host expects data entry to happen. The neutral contract should preserve the semantic distinction without publishing private host-specific mode names.

Common host patterns include:

```text
Direct grid editing
    → the grid itself is the editing surface; configured cell/row add/edit/delete may be available

Same-form controls
    → controls surrounding the grid perform add/edit/delete for the selected row

Dedicated editor form/dialog
    → a separate input surface performs add/edit/delete for the selected row
```

These are interaction patterns, not authorization grants.

Action/settings metadata may describe capabilities such as add, edit, delete, read, search, export, print, report, preview, or view-only behavior. Grid configuration may separately describe whether rows/cells are editable or whether add/remove actions are exposed.

The adapter must expose the actual supported capability for the current host state rather than assuming that one edit pattern implies another. A host may allow row creation but require a dedicated editor for modification, or expose only a subset of editable fields.

Host interaction configuration never bypasses Hive authorization.

## 9. UI operations versus business operations

These are separate contracts.

### UI operations

Examples:

```
ReadControl
SetControlValue
SelectLookupValue
AddGridRow
EditGridRow
DeleteGridRow
InvokeHostAction
```

### Business operations

Examples:

```
CreateInvoice
UpdateInvoice
SaveOrder
PostDocument
```

A UI operation must not silently acquire business semantics.

Likewise, a business operation must not be modeled as a sequence of arbitrary UI clicks when the host exposes an authoritative business API.

## 10. API/UI composition

V1 supports:

```
API only
UI only
API + UI
```

The model should not be presented with an unrestricted "choose API or UI" switch.

Instead, the host exposes an authorized operation capability, and the operation adapter selects the concrete implementation.

Examples:

```
CreateInvoice
    implementation = business API

LegacyLookup
    implementation = UI

CreateInvoiceWithLegacyAttachment
    implementation = API + UI
```

The implementation mechanism is transparent to authorization and audit.

When API and UI paths are combined, one operation correlation identity must cover the complete logical operation.

## 11. Business operation proposal

A consequential business operation should be represented as a structured proposal before execution. The host application registers or exposes the logical operation capability through the Hive boundary; the model does not invent an arbitrary business operation name and gain permission merely by requesting it.

Conceptually:

```
BusinessOperationProposal
├── OperationId
├── WorkItemId
├── OperationType
├── TargetEntity
├── ParentData
├── ChildData[]
├── identity/lookup references
├── intended changes
├── provenance
├── idempotency/correlation identity
└── expected host state/version where available
```

The proposal is the object that reaches Hive authorization and, where policy requires it, the existing V1 Approval boundary. Its logical `OperationId` remains stable across retries/reconciliation of the same intended operation; a retry does not create a new logical operation merely because execution is repeated.

Hive does not authorize a raw control click merely because the model requested one.

## 12. Business-operation receipt

Every consequential operation attempt that is submitted to the host boundary must have durable attempt/receipt evidence, including successful, rejected-before-mutation, partially applied, known-failed, and unknown outcomes.

Phase 1.17 establishes the logical `OperationId` and the initial durable operation-attempt state required before a non-transactionally coupled host submission. Phase 1.18 completes that attempt into the durable `BusinessOperationReceipt`, records the final disposition when known, and owns unknown-outcome reconciliation. A crash or transport break after submission but before a host response therefore leaves a durable recovery anchor rather than an untracked host call.

The receipt is not merely a success boolean and must not be replaced by the WorkItem status alone.

Conceptually:

```
BusinessOperationReceipt
├── OperationId
├── WorkItemId
├── Host/Application identity
├── Adapter/implementation identity
├── Operation type
├── Parent target identity
├── resulting parent identity when changed
├── Child target identities[]
├── resulting child identities when changed
├── Host correlation/transaction identifier when available
├── completed-at timestamp
├── result/status
└── host version/concurrency evidence when available
```

For an invoice example:

```
Operation = CreateInvoice
Parent:
    InvoiceId = 1842

Children:
    InvoiceLineId = 9011
    InvoiceLineId = 9012
    InvoiceLineId = 9013
```

The host remains the authoritative source of those records. The receipt is Hive's durable attribution of what operation occurred.

### 12.1 Why IDs are required

The affected host identities allow Hive to:

- link later review to the exact records written;
- read the resulting host state for verification;
- reconcile retries;
- detect duplicate/partial effects;
- explain the outcome to a user;
- recover an interrupted workflow without blindly repeating a write.

When the host uses generated identity values, the host adapter is responsible for obtaining them through an authorized mechanism.

### 12.2 Receipt disposition and partial success

A receipt records the durable disposition of the attempted host operation, not merely successful writes. It should distinguish at least:

- not executed/rejected before host mutation;
- completed successfully;
- partially applied;
- failed with a known no-side-effect result;
- unknown outcome after interruption or transport failure.

Parent/child operations may partially succeed when the host boundary is not atomic. The receipt therefore records the identities actually returned or otherwise established, plus the operation correlation/idempotency identity.

An unknown outcome must not automatically trigger a duplicate write. Recovery first attempts reconciliation through the operation identity and host-side state; a second mutation requires an explicit idempotent/reconciliation decision.

Where the host can enforce idempotency, Hive should reuse the same logical operation identity on retry. Where the host cannot, the receipt and host-state reconciliation are the source of truth for deciding whether a retry is safe.

### 12.3 Durable attempt and reconciliation boundary

A non-transactional host call cannot rely on Hive and the host application sharing one database transaction. Therefore the implementation must establish a durable operation identity before the call and persist enough attempt state to determine, after an interruption, that a host call was in flight or may already have taken effect.

Recovery must:

1. load the durable attempt/receipt by the same logical `OperationId`;
2. reconcile against any host-side idempotency/correlation evidence;
3. reread authoritative host state when necessary;
4. classify the disposition before deciding whether another mutation is safe.

A second host mutation is never justified merely because the caller did not receive a response. Where the host supports idempotency, the same logical operation identity is reused. Where it does not, reconciliation must establish a safe retry condition first.

## 13. First-class post-write Review

Approval and Review are separate concepts.

### Approval

```
Should Hive perform the proposed consequential operation?
```

### Review

```
Did the resulting host state contain the intended data correctly?
```

A review can therefore exist even after an approved and successfully completed write.

The V1 architecture treats Review as a first-class, provenance-bearing WorkItem-linked object rather than a UI-only flag.

Conceptually:

```
Review
├── ReviewId
├── WorkItemId
├── OperationId / Receipt reference
├── Status
├── Verification method
├── reviewer identity when human
├── evidence
├── discrepancy set
├── created/completed timestamps
└── review version/concurrency information
```

### 13.1 Review methods

The contract should support:

```
Human
Automated
Hybrid
```

The minimum V1 implementation requirement is first-class human review after a governed business write when review policy requires it.

Automated verification may perform a host-side read/compare before presenting a human task. When human review is required, the reviewer should be able to open or navigate to the associated host record/editor through a bounded authorized host capability, inspect the authoritative host state, and record the review result/evidence. A human may still be required for discrepancies or higher-risk operations.

### 13.2 Review policy

Review is policy-governed; it is not automatically mandatory for every operation. A review requirement is a Hive/host policy decision, never a decision produced by the model.

A host/application policy may require:

- no review for a low-risk operation;
- automated verification;
- mandatory human review;
- human review only when automated verification finds discrepancies.

The policy is enforced in code and is never inferred from model output.

### 13.3 Verification source of truth

The host application's authoritative state is the verification source.

The normal flow is:

```
intended candidate data
        +
BusinessOperationReceipt
        ↓
authorized host read
        ↓
bounded comparison
        ↓
Review outcome
```

Review is specifically host-operation correctness evidence. It is not the same semantic as CognitiveAgent Success or Mistake, which evaluate whether a CognitiveAgent's objective or success criteria were satisfied for cognitive learning.

A completed Review may contribute evidence to a CognitiveAgent OutcomeEvaluation only through an explicit, provenance-bearing reconciliation path. A human correction recorded by Review is evidence with an attributable source; the Review state itself remains owned by the V1 host/work-operation boundary.

Hive may store the minimum evidence required to explain and audit the review. It must not silently become a mirror of the host business database.

### 13.4 Review outcomes

The V1 minimum outcomes are:

```
VerifiedCorrect
VerifiedIncorrect
```

The architecture may also represent:

```
PendingReview
VerificationUnavailable
Inconclusive
```

as operational states when the verification boundary cannot complete.

A review finding must preserve the discrepancy rather than silently rewriting the intended data.

## 14. End-to-end V1 data-entry lifecycle

The intended V1 end-to-end architecture is:

```
Input submission
  ↓
Input-specific preparation / routing
  ↓
Structured candidate
  ↓
Validation
  ↓
Business-operation proposal
  ↓
Authorization
  ↓
Approval (when required)
  ↓
Host operation
  ↓
Business-operation receipt
  ↓
Review policy
  ↓
Automated verification and/or human review
  ↓
Review result
```

This separates:

- what Hive believed should be entered;
- whether Hive was permitted to enter it;
- what operation actually occurred;
- which host records were affected;
- whether the resulting state is correct.

## 15. Provenance, staleness, and concurrency

Every consequential host operation and review must remain attributable to:

- WorkItem;
- operation identity;
- caller/principal;
- host/application registration;
- adapter/implementation;
- relevant execution/runtime identity;
- timestamps;
- expected host version/concurrency token when available.

A stale host context or disposed control must not silently receive a mutation.

The V1 neutral contract does not require an ETag/version token. A host may expose meaningful concurrency/version evidence when it has one, but a host without such evidence may use its ordinary save behavior.

Where a host provides a meaningful concurrency/version value, its contract implementation may expose it as evidence. Where it does not, absence of such evidence is simply the host's current concurrency model and is not represented as a detected conflict.

## 16. Security boundary

The host adapter is a translator and bounded executor, not an authorization authority.

The following never grant Hive permission by themselves:

- control visibility;
- `Enabled`;
- `ReadOnly`;
- `AllowEdit`;
- `AllowDelete`;
- hidden primary-key columns;
- database/table/field names;
- lookup filter strings;
- host-side permission switches.

Authorization is checked by Hive code before the consequential operation.

The model never receives:

- database credentials;
- unrestricted SQL;
- raw host object handles when a semantic contract is sufficient;
- arbitrary method invocation;
- secret fields.

## 17. Contract-first host implementation model

Phase 1.14 is contract-first, but the host-facing WinForms experience is intentionally implementation-assisted. Hive owns the neutral public integration contracts and provides reusable WinForms base forms/controls on top of those contracts. The host application supplies only the application-specific semantics and behaviors that Hive cannot safely infer.

The intended ownership model is:

```text
Hive.Core
  ↓
Hive-owned host integration contracts

Hive WinForms integration
  ↓
reusable HiveForm / Hive control implementations
default metadata, discovery, capability, lifecycle, and safety plumbing

Host application
  ↓
inherits/uses Hive WinForms base types where appropriate
supplies explicit overrides and application-specific semantics

Hive Management / integration runtime
  ↓
discovers, composes, authorizes, and invokes bounded capabilities
```

A host does not need to manually construct every neutral descriptor for every standard WinForms control. The Hive WinForms layer should project standard control semantics automatically and expose explicit override points for cases where application meaning cannot be inferred safely.

A host may still implement or adapt a Hive contract directly on an application-owned type where that is the simplest design. Base Hive controls are a convenience and reuse mechanism, not a replacement for the neutral contract boundary.

### 17.1 Reusable WinForms base implementation

Hive should provide a bounded set of reusable WinForms base forms/controls where inheritance materially reduces host integration work without creating a second UI toolkit.

The intended pattern is:

```csharp
public class HInvoiceForm : HiveForm
{
}

public class InvoiceForm : HInvoiceForm
{
    // ordinary WinForms application behavior
}
```

Common Hive controls may be used directly:

```csharp
HiveTextBox
HiveComboBox
HiveCheckBox
HiveDateTimePicker
HiveNumericUpDown
HiveDataGridView
```

A host may derive its own reusable application control from a Hive base control when it needs application-specific behavior, but it should not need a one-off wrapper class merely to obtain the common Hive integration behavior.

The reusable layer is responsible for mechanics that are safe and generic, including:

- bounded Form/control discovery and host-context registration;
- standard value adaptation;
- deterministic default control/field/surface identity;
- common metadata projection;
- capability plumbing for supported standard interactions;
- row/field/lookup plumbing where semantics are standard;
- lifecycle, cancellation, provenance, and disposal integration;
- deterministic contract validation and failure handling.

The base layer must not guess authoritative business meaning merely because a UI shape looks familiar. It must not infer arbitrary database relationships, invent business operations, or turn control visibility/enabled state into authorization.

### 17.2 Automatic defaults and explicit overrides

The host integration follows a convention-first model:

```text
Safe standard convention
        ↓
automatic Hive behavior
        ↓
explicit host override when required
        ↓
Hive authorization remains authoritative
```

Examples of information that may be provided automatically when deterministic:

- field name from a standard control identity;
- surface identity from a Hive form/data-surface convention;
- value kind from the standard control type;
- stable capability identity generated by Hive;
- standard read/set behavior for supported controls.

Examples that may require explicit host information:

- authoritative primary-key field;
- parent/child relationship;
- application-specific generated/computed semantics;
- dependent lookup meaning;
- business action identity and implementation;
- host-specific validation or save semantics.

Defaults must be deterministic, documented, and overridable. An override changes semantic metadata; it does not grant authorization.

### 17.3 Host context

The host root contract may provide the bounded context Hive needs to understand a registered Form/application surface.

Conceptually:

```text
Host
 ├── Form/context identity
 ├── semantic controls
 ├── data surfaces
 ├── fields
 ├── lookups
 └── bounded actions/capabilities
```

A Hive WinForms base form should make the common registration/context path automatic where possible. The host supplies explicit context only where application-specific meaning or lifecycle cannot be inferred safely.

Hive may then discover the registered surface and use the semantic contracts directly. Raw WinForms controls, arbitrary reflection, SQL, credentials, and unrestricted method invocation remain outside the neutral public contract.

### 17.4 Existing-control compatibility

The Hive base-control layer must not be mandatory for all host applications.

Existing applications may already have:

- ordinary native WinForms controls;
- application-owned custom controls;
- third-party control libraries;
- forms that cannot inherit from `HiveForm`.

Those applications remain integrable through the bounded `Hive.Host.WinForms` adapter and semantic-provider path. The same neutral contracts and Hive Management authorization boundary apply.

This gives Hive two supported integration paths:

```text
Preferred low-code path
Host
  ↓
HiveForm / Hive controls
  ↓
neutral contract
  ↓
Hive Management

Compatibility path
Host controls/form
  ↓
WinForms adapter / semantic provider
  ↓
neutral contract
  ↓
Hive Management
```

The two paths must produce the same contract semantics and security boundary where they describe the same host capability.

### 17.5 Public boundary

Public Hive documentation describes the neutral contracts, reusable WinForms base behavior, supported defaults, override points, guarantees, lifecycle, and security boundaries. A host application's private classes, methods, source code, database schema, control library, query text, and private business conventions remain outside the Hive repository's architectural contract.

Hive's reusable base controls and forms are Hive-owned implementation types. Host applications may derive from them, compose them, or bypass them through the compatibility adapter. Hive must not require host applications to expose their private business frameworks as public Hive types.


### 17.6 Concrete WinForms base integration surface

The preferred WinForms path uses a small, bounded set of Hive-owned native-control-derived types. These types add integration metadata and safe conventions; they do not replace the WinForms control model or move host business logic into Hive.

The Phase 1.14 V1 base types are:

```
HiveForm : Form
HiveTextBox : TextBox
HiveComboBox : ComboBox
HiveCheckBox : CheckBox
HiveDateTimePicker : DateTimePicker
HiveNumericUpDown : NumericUpDown
HiveDataGridView : DataGridView
```

Each base type exposes Hive-owned integration metadata while preserving the normal WinForms API. A base type therefore does not require a host-side wrapper whose only purpose is to obtain Hive integration behavior.

The metadata surface is intentionally bounded:

- control identity may be explicitly overridden;
- field controls may override field name, binding member, value type, required/read-only state, generated/computed state, primary-key state, and lookup metadata;
- data surfaces may override surface identity/name, primary-key field, and explicit parent/child relationship metadata;
- individual data-surface fields may receive the same explicit semantic overrides;
- `HiveForm` may override the host display identity while retaining the normal WinForms form lifecycle.

The default precedence is:

```
explicit Hive metadata
        ↓
safe deterministic WinForms convention
        ↓
bounded path fallback
```

For control identity, an explicit control identity wins; otherwise a unique non-empty WinForms `Name` is used; otherwise the deterministic control path is used. Duplicate explicit/derived identities that would make the contract ambiguous must fail capture rather than silently alias two controls.

For field identity, an explicit field name wins; otherwise a bound member is used; otherwise a control name is used; the bounded path is the final fallback. For a data-grid column the equivalent order is explicit field name, `DataPropertyName`, then column name. Required, generated, computed, and primary-key state defaults to false unless the standard control semantics provide a reliable read-only state; host-specific semantic flags are explicit overrides. When a field is explicitly marked as a primary key, including through a configured data-surface primary-key field, the captured field is represented as read-only so stable identity is not exposed as an editable value.

For data-surface identity, an explicit surface identifier wins; otherwise a unique non-empty control name is used; otherwise the deterministic control path is used. A child surface may explicitly name its parent surface and the parent/child key fields. The adapter materializes the corresponding parent-side `HiveHostChildDataSurfaceDescriptor`; it does not infer arbitrary business relationships from layout.

Capability identities generated from automatic behavior are deterministic for the canonical adapter/control/surface identity and capability kind. Capability identifiers remain identifiers, not authorization grants: Management authorization is still required before any consequential interaction.

`HiveDataGridView` exposes field metadata and stable-row identity configuration, but it does not become a generic data-access or business-write engine. Row mutation, host validation/save behavior, lookup resolution, and business/application actions remain host-owned through the bounded semantic-provider/operation hooks. The base control only supplies reusable contract metadata and safe standard interaction plumbing.

Base metadata is owned by the host control/form instance. Disposing the adapter or host-integration registration does not dispose host controls or forms and does not invalidate host-owned application lifecycle beyond the registration itself.

## 18. Non-goals for V1



This architecture does not authorize:

- direct Hive access to the host database;
- generic SQL execution by the Agent;
- a universal UI automation framework;
- arbitrary host reflection;
- automatic inference of all business relationships from visual layout;
- treating UI action capability as business authorization;
- copying the host application's entire domain model into Hive;
- mandatory human review of every operation;
- reopening Phase 1.13.

## 19. Phase ownership

```
1.13
    concrete WinForms discovery/read-only host context

1.14
    neutral host integration contracts
    reusable WinForms base form/control layer
    deterministic defaults and explicit overrides
    WinForms adapter compatibility path
    bounded UI/data-surface interaction
    stable identities
    lookup semantics
    API/UI composition
    authorization/provenance

1.15
    input preparation and routing
    image → vision capability
    spreadsheet → structured row mapping

1.16
    structured candidate extraction and validation
    (parent/child candidate structure only where the V1 operation requires it)

1.17
    business write
    operation proposal
    authorization / approval

1.18
    durable business-operation receipt
    unknown-outcome reconciliation
    first-class post-write Review

1.19
    MAF Sequential composition of the V1 pipeline

1.20
    full-pipeline crash/recovery including write receipt/review recovery

1.21
    metrics
    budget cap
    OpenTelemetry
```

Phase 7 later generalizes the proven host concepts to meaningfully different host technologies.

## 20. Implementation freeze rule

The Phase 1.14 contract should be frozen around semantics that are explicitly part of the supported V1 host-integration boundary. Discovery of a particular host implementation is not itself a reason to add that host's private types, helpers, persistence details, or conventions to Hive.

The supported contract boundary includes:

- explicit parent/root and child-collection relationships when supplied by the host;
- parent-identity propagation where the host operation requires it;
- host-owned validation and pre-save veto points;
- host save/result/reload behavior exposed through bounded capabilities;
- New/Edit and related host lifecycle behavior;
- child data-surface editing and add/edit/delete lifecycle patterns;
- primary-key ID row identity for the current V1 contract and positional row-index handling;
- generated/computed field semantics;
- bounded lookup resolution with relevant host-provided context;
- bounded host actions such as New, Edit, Save, Delete, Reload, Move, Search, Report, Print, and Preview when supported by the host.

Concrete adapter/base-control types should be frozen only after the neutral contracts, default/override semantics, capability boundaries, lifecycle/disposal rules, and contract tests are defined. The base-control layer must remain bounded: add a Hive base control only when it provides reusable integration semantics or UI behavior that is genuinely owned by Hive; do not create wrappers solely to rename every WinForms control.

Do not recreate host database or business frameworks in Hive when the host already owns the authoritative behavior.
The goal is to adapt existing host semantics into Hive's neutral boundary, not to recreate the host's data-access or business framework.
