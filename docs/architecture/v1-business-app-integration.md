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

The first concrete implementation remains the V1 WinForms boundary. Generic cross-host technology support remains Phase 7 work; the neutral contracts in this document exist so the V1 WinForms adapter itself is not vendor/control-library-specific.

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

A business operation may be implemented through an API/service, a UI workflow, or a composition of both. The implementation mechanism must not redefine the business meaning.

## 3. Neutral public extension contracts

Hive should ship public, host-neutral contracts for the concepts required by V1. The exact type/interface names are implementation decisions for Phase 1.14, but the responsibilities are fixed by this architecture.

The public contract family must support at least:

- a host integration registration/adapter boundary;
- semantic control descriptors;
- data-source/data-surface descriptors;
- field/column descriptors;
- stable row identities;
- lookup descriptors and bounded lookup operations;
- bounded interaction operations;
- business-operation capabilities;
- write receipts;
- review records and review evidence.

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

### 5.2 Established host-provided parent/child mapping

A real production host inspected for V1 establishes the pattern the neutral contract must represent without exposing the host application's implementation types.

Conceptually:

```text
root data surface
      ↓
explicit child-collection metadata
      ↓
child data surface
      ↓
parent identity → child foreign-key relationship
```

The relationship is host-provided and authoritative for the adapter. It must not be inferred merely from visual nesting, similar names, hidden fields, or arbitrary database metadata.

The adapter may translate host-provided relationship metadata into Hive's semantic parent/child data-surface contract. Host-specific deletion rules, filtering/partition context, data containers, and persistence helpers remain inside the adapter or host business layer.

For create operations, the host may propagate the parent identity into new child rows. For load and edit operations, the adapter may expose the resulting parent/child data surfaces without exposing the host's database objects, generated SQL, or binding containers.

## 6. Stable row identity

Stable identity is mandatory for consequential row operations.

The preferred identity order is:

1. explicit primary key;
2. explicit composite key;
3. host-defined stable row identity;
4. bound-object identity when the host contract guarantees its stability for the operation.

Row index is not an authoritative identity.

```
row index = positional address
row identity = stable record identity
```

An operation that begins from a row index must resolve that row to a stable identity before a consequential mutation is committed. The identity used to locate a record does not have to remain unchanged after the operation: a host may permit a key value to change as part of an update. Hive must therefore retain the authoritative pre-operation identity for locating the target, and the host adapter should report the resulting identity when the operation changes it.

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

### 6.2 Composite identities

The contract must support multiple key parts:

```
RowIdentity
├── BranchId = 3
└── OrderId = 5812
```

A single-key host simply provides one key part.

### 6.3 Generation semantics

Generated fields are host-owned outputs.

Examples:

- identity/sequence IDs;
- creation timestamps;
- host-generated document numbers.

Hive must not invent generated values merely because an add-row operation requires the field.

The write result should return the host-generated or resulting identity when the host can provide it. When an authorized update changes the host key, the operation must retain the authoritative pre-operation identity used to locate the target and report the resulting identity separately.

### 6.4 Computed fields

Computed fields are readable host outputs and are not directly writable through generic field mutation.

Examples:

- calculated amount;
- total;
- derived status.

A computed field may be used as evidence/input to reasoning, but direct mutation requires an explicit host/business operation contract.

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

Action/settings metadata may describe capabilities such as add, edit, delete, read, search, export, print, report, or view-only behavior. Grid configuration may separately describe whether rows/cells are editable or whether add/remove actions are exposed.

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

Every consequential operation attempt that is submitted to the host boundary must have a durable receipt/attempt record, including successful, rejected-before-mutation, partially applied, known-failed, and unknown outcomes.

For a host boundary that is not transactionally coupled to Hive persistence, the logical operation identity and an initial durable attempt record must be persisted before submission whenever needed to make interruption/reconciliation safe. The final receipt disposition is then recorded after the host reports an outcome; a crash or transport break after submission but before a host response leaves the durable attempt in an unknown/reconcilable state.

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

The completed architecture is:

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
result
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

A stale host context, disposed control, changed row identity, or changed authoritative business record must not silently receive a mutation.

Where the host supports optimistic concurrency, the adapter should carry an expected host version/ETag/revision through the operation.

Where the host provides no concurrency token, the adapter must use the strongest stable identity and current-state check that the host contract can guarantee; lack of concurrency evidence must not be represented as proof of correctness.

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

## 17. Host-specific implementation evidence and public-document boundary

The V1 host-integration design was informed by inspection of a real production WinForms application with application-owned controls and bound parent/child data surfaces. That inspection establishes implementation evidence for the adapter; it does not establish a public Hive dependency.

### 17.1 Established host semantics

The inspected host establishes these reusable semantic patterns:

- an explicit root-to-child data relationship supplied by host metadata;
- parent identity propagation into child rows during create;
- host-owned required/unique validation and veto points before a save;
- a host business/save boundary followed by an authoritative result or reload;
- editable child collections that can be changed in the host data surface before the surrounding business save;
- multiple UI edit-surface patterns, including direct grid editing, same-form supporting controls, and dedicated editor forms/dialogs;
- host-generated record identity returned after creation;
- positional row addresses that are insufficient as persisted identity;
- selection lists that can synchronize selection state from bound data;
- semantic field/column metadata for binding, type, key, generation, nullability, requiredness, computed values, and lookup behavior.

These semantics are useful adapter inputs. They are not Hive business logic, database contracts, or authorization grants.

### 17.2 Public documentation boundary

Public Hive documentation intentionally describes the neutral semantics and contracts, not the private implementation that happened to provide the evidence.

Do not publish:

- private host class, interface, property, method, or event names;
- source-code excerpts from a private host application;
- host-specific field/control mappings that are not part of Hive's neutral contract;
- private business conventions merely because an adapter currently uses them;
- host-generated SQL or persistence implementation details.

The intended relationship is:

```text
private host source
      ↓
host adapter / design knowledge
      ↓
neutral Hive contract
      ↓
public Hive documentation
```

A future public adapter may document its own public integration contract when that contract is intentionally part of Hive's supported surface.

### 17.3 Remaining adapter-fidelity questions

Before freezing concrete Phase 1.14 adapter types, the remaining host-specific questions are limited to the adapter implementation boundary:

- exact semantic value access required by the neutral descriptor;
- exact field/column metadata and runtime mapping from a bound row to stable persisted identity;
- generated/computed-field behavior during create and edit serialization;
- complete existing-child edit serialization;
- bounded lookup execution and result mapping;
- host concurrency/version behavior;
- any host action contract actually required by V1.

These questions remain implementation inputs and must not be promoted into public Hive architecture merely because they were observed in one private host implementation.

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
    WinForms adapter
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
    approval
    durable business-operation receipt
    first-class post-write Review

1.18
    MAF Sequential composition of the V1 pipeline

1.19
    full-pipeline crash/recovery including write receipt/review recovery
```

Phase 7 later generalizes the proven host concepts to meaningfully different host technologies.

## 20. Implementation freeze rule

The inspected production host is sufficient evidence for the following host-level semantics and they should no longer be treated as open discovery questions:

- explicit parent/root and child-collection relationships;
- parent-identity propagation into child rows where the host operation requires it;
- host-owned required/unique validation and pre-save veto points;
- the host business/save boundary and post-save result/reload behavior;
- New/Edit and related host lifecycle behavior;
- host permission/logging settings as host behavior rather than Hive authorization;
- child data-surface editing and add/edit/delete lifecycle patterns.

### 20.1 Freeze evidence summary

The detailed host inspection establishes:

- bound child-data editing and synchronization;
- distinct direct-grid, same-form-control, and dedicated-editor interaction patterns;
- host validation and veto points around consequential row changes;
- in-memory child-row mutation before the surrounding business save;
- positional row selection as non-authoritative identity;
- a concrete single-key identity in the inspected V1 surface, without making that identity shape a Hive-wide requirement.

Before freezing the concrete Phase 1.14 adapter types, the remaining implementation-specific adapter questions are:

- exact neutral field/column metadata and value access needed by the adapter;
- runtime mapping from a bound row to its stable persisted identity; row position remains non-authoritative;
- exact generated/computed-field behavior and edit serialization for existing child rows where the complete host path is not yet established;
- exact bounded lookup resolution behavior;
- exact concurrency/version behavior where the host exposes it;
- any host action surface the adapter must expose or invoke, only where its production semantics are established.

Do not recreate these host mechanisms in Hive when the host already exposes an authoritative contract.

The goal is to adapt existing host semantics into Hive's neutral boundary, not to recreate the host's data-access or business framework.
