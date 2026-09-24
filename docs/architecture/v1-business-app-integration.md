## 17. HForms-specific evidence without HForms coupling

The production source supplied for HForms demonstrates why the neutral contract must be richer than ordinary `DataGridView` metadata while still remaining independent of HForms.

### 17.1 Host object relationships are established

The HForms relationship is not inferred from visual nesting alone.

The production implementation explicitly maps:

```
HDataBox.MainTable
        ↓
TableInfo.ChildTable[]
        ↓
child TableInfo.TableName
        ↕
HDataGridView / HList.DataSourceName
```

When reading data, HDataBox maps the corresponding child `DataTable` into the matching bound grid/list.

When preparing an insert, HDataBox iterates the child data surfaces and writes the parent primary-key value into the child foreign-key field. The host therefore supplies an explicit parent → child relationship that the Hive adapter can translate into a semantic parent/child data-surface relationship.

This is evidence for the neutral contract. It does not justify exposing `TableInfo`, `DataSet`, `DataTable`, or HForms objects as Hive public API.

### 17.2 HDataBox save lifecycle is established

The production save path is:

```
user Save
   ↓
binding/form validation
   ↓
required-field validation
   ↓
unique-field validation
   ↓
host CheckBeforeSave veto
   ↓
host SaveRecord operation
   ↓
PerformAfterSave(ID)
   ↓
reload authoritative record
   ↓
host logging
```

The exact database/business operation behind `SaveRecord` remains host-owned. This is important for the Hive architecture: Hive needs an authorized **business-operation capability**, not unrestricted access to HDataBox's generated SQL or internal persistence helpers.

### 17.3 New/Edit and generated identities

HDataBox has explicit New/Edit modes and invokes the same host save lifecycle for each.

For New, the host may generate a record identity/code and later expose the resulting identity through `PerformAfterSave(ID)`. HDataBox then reloads the newly created record through its normal navigation path.

Therefore the adapter contract must support:

- new-row state before persistence;
- host-generated identity returned after create;
- authoritative record reload after create/update;
- separation between positional navigation and record identity.

The neutral contract must not assume that every identity is an immutable numeric auto-increment value.

### 17.4 Identity stability versus identity immutability

A row identity must be stable **for the duration and purpose of the consequential operation**, but the host may permit the key value itself to change as part of an update.

For example:

```
before operation:
    key = A

intended update:
    key = B
    other fields = ...

```

The adapter must retain the authoritative pre-operation identity (and host concurrency/version evidence when available) to locate and protect the original record. The new key value is an intended field change, not the basis for locating the pre-update row.

This permits single-key, composite-key, host-defined, generated, and mutable-key models without treating row position as identity.

### 17.5 Host validation and business boundaries

HDataBox supplies concrete host validation points:

- required fields;
- unique fields;
- `CheckBeforeSave` custom business/application veto;
- host-controlled save/update operation;
- post-save callback and reload.

These are host semantics. Hive should request a declared business operation and let the host remain authoritative over validation and business rules. Hive may perform earlier candidate validation in Phase 1.16, but successful Hive validation must never be treated as proof that the host will accept the write.

### 17.6 ByAlone, ByControls, and ByForm

The neutral adapter must preserve the host's edit-surface distinction.

```
ByAlone
    → the grid itself is the editing surface; add/delete/direct cell editing may be available.

ByControls
    → controls on the same form as the DataGridView perform add/edit/delete for the selected grid row.

ByForm
    → an input dialog/form is the editing surface for add/edit/delete.
```

These are interaction modes, not authorization. The adapter may expose the resulting supported UI capabilities, but Hive authorization decides whether a caller may use them.

### 17.7 HForms components remain adapter inputs

The host-specific relationship is therefore:

```
HDataBox / HActionBar / HDataGridView / HList / TableInfo
                         ↓
                Hive neutral semantics
```

The adapter translates:

- host lifecycle and action availability;
- parent/child data-surface relationships;
- field/column metadata;
- stable row identity;
- generated/computed semantics;
- lookup behavior;
- bounded UI interaction.

It must not make Hive understand HForms itself.

# Hive Architecture — V1 Business-App Integration

This document is part of the authoritative architecture defined by `docs/architecture.md`. It contains the detailed V1 host-adapter, business-data, business-operation, write-receipt, and post-write review contracts.

## 1. Purpose and boundary

V1 automates data entry from an image into an existing business application. The business application's domain model, database, validation rules, transactions, and authoritative records remain owned by that application.

Hive therefore needs an integration boundary that can understand enough of a host application to perform a governed operation without becoming coupled to one control library, ORM, database schema, or UI framework.

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
- HForms/HControls;
- another developer's control library;
- another host integration implementation added later.

Hive must not make HForms, HControls, `IHyperControl`, `HDataBox`, `HActionBar`, `TableInfo`, or any other application-specific type a platform dependency.

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
- `IHyperControl` or HControls types;
- `DataTable`/ORM-specific types as required public contracts;
- `SqlConnection`, SQL commands, or raw SQL expressions;
- provider credentials;
- arbitrary host object references;
- unrestricted reflection or arbitrary method invocation.

The concrete WinForms adapter may internally use the host APIs required to translate or perform an authorized bounded operation. That does not transfer database, business, or authorization ownership into the adapter.

### 3.1 Contract placement

Pure host-integration semantics belong in `Hive.Core` because they must remain dependency-light, host-neutral, and usable by any host adapter. They may reference other pure Core contracts such as Hive identities and resource references, but they must not reference WinForms, HForms, UI controls, SQL providers, or MAF implementation types.

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

### 5.2 HForms/TableInfo mapping

The existing HForms pattern provides useful evidence for what the neutral contract must be able to represent.

Conceptually:

```
TableInfo.MainTable
        ↓
root business/data entity

TableInfo.PkName
        ↓
stable record identity

TableInfo.ChildTable[]
        ↓
related child data sets

TableInfo.DeleteType / VoidFieldName
        ↓
host-specific deletion semantics

UseYear / UseBranch / related filters
        ↓
host-side data-selection/business context
```

The adapter must translate those concepts into Hive semantics.

It must not expose `TableInfo` itself or its generated SQL methods.

In particular, methods that generate `INSERT`, `UPDATE`, `DELETE`, or `SELECT` statements remain host implementation details. Hive does not receive an unrestricted SQL execution contract.

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

An operation that begins from a row index must resolve that row to a stable identity before a consequential mutation is committed.

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

The write result should return the host-generated identity when the host can provide it.

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

```
Lookup
├── identity
├── display field
├── value field
└── bounded filtering/query capability
```

HForms properties such as:

```
FillTableName
FillDisplayFieldName
FillValueFieldName
FillFilterQuery
```

may be translated into that semantic model.

`FillFilterQuery` must never be exposed to the model as executable SQL.

The host adapter owns actual lookup execution and returns bounded options such as:

```
value = 42
display = "Product A"
```

The model can choose an option; it cannot execute arbitrary lookup SQL.

## 8. UI edit modes and operation capabilities

UI configuration may describe how the host expects data entry to happen.

Examples include:

```
ByAlone
ByControls
ByForm
```

and action/settings metadata such as:

```
AllowNew
AllowEdit
AllowDelete
AllowRead
AllowSearch
AllowExport
AllowPrint
AllowReport
AllowPermissionCheck
AllowUserLogHandling
AllowViewLog
```

Grid-specific configuration can also expose behavior such as:

```
AllowToAddRows
AllowToRemoveRows
editable-column configuration
```

These values describe available, intended, or configured host UI behavior. They are not Hive authorization grants.

The adapter must expose actual supported capabilities based on the current host state rather than assuming every grid supports direct cell/row mutation. For example, a grid may allow adding rows but require a surrounding editor for modification, or may expose only a subset of editable columns.

```
ByAlone
    → direct grid row/cell operations may be supported

ByControls
    → edit may require surrounding controls

ByForm
    → edit may require a form-level workflow
```

The exact meaning is host-defined and is translated by the adapter.

The host's edit-mode and action configuration must therefore be translated into bounded capability metadata and, where an operation is supported, a concrete host operation contract. It must never be treated as a permission bypass.

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
├── Parent identity
├── Child identities[]
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

Automated verification may perform a host-side read/compare before presenting a human task. A human may still be required for discrepancies or higher-risk operations.

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
Image
  ↓
Extraction
  ↓
Typed candidate
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
VerifiedCorrect / VerifiedIncorrect / unresolved
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

### 17.1 HForms configuration is evidence, not a second Hive contract

The supplied production HForms/HControls source establishes the host-side lifecycle and data-entry conventions that the V1 adapter must be able to translate.

`HDataBox : UserControl` combines:

- CRUD/action flags such as `AllowNew`, `AllowEdit`, `AllowDelete`, `AllowRead`, `AllowSearch`, `AllowExport`, `AllowPrint`, `AllowReport`, `AllowViewLog`;
- permission/logging behavior such as `AllowPermissionCheck` and `AllowUserLogHandling`;
- binding state such as `BindingControl`, `Bs`, and the loaded `DataSet`;
- presentation/application metadata such as `TitleEn`, `TitleAr`, `LanguageType`, and `CodeType`;
- reporting configuration that remains host-owned;
- application/data conventions such as `VoidFieldName`, branch, and year handling.

The adapter should project only semantics required for a Hive-authorized operation. Reporting, printing, code-generation conventions, logging implementation, and other application-specific metadata remain host-owned unless a concrete V1 capability requires them.

The supplied `HDataBox` implementation is now sufficient evidence for the host lifecycle; it is not merely a property list. It establishes:

- `MainTable` as the root `TableInfo` for the bound business record;
- `TableInfo.PkName` as the current production HForms record key; in this host model the key is a single integer key, not a composite key;
- `MainTable.ChildTable` as the authoritative child-table collection used by HDataBox;
- child `HDataGridView`/`HList` association through matching `DataSourceName`;
- parent-key propagation into child rows through `MainTable.PkName`;
- host-side required/unique validation before save;
- `CheckBeforeSave` as an explicit host veto/boundary before persistence;
- `SaveRecord` as the save operation event, allowing the host application to own the actual persistence/business implementation;
- `PerformAfterSave(ID)` as the post-save generated-record identity callback;
- New/Edit lifecycle transitions followed by record reload;
- `AllowPermissionCheck` branch/year conventions as host data-selection/write conventions, not Hive authorization.

The implementation therefore proves that HDataBox is an orchestration/container around binding, validation, child-data preparation, save/update lifecycle, and host callbacks. Hive must adapt that lifecycle; it must not recreate it or interpret its host flags as Hive permission.

The newer `HActionBar : Control` should only be inspected further when its concrete contract is needed by the adapter. Its existence is not a reason to keep the already-established HDataBox lifecycle open as an unknown.

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
    vision routing

1.16
    structured extraction and validation
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

The supplied production `HDataBox` source is sufficient evidence for the following host-level semantics and they should no longer be treated as open discovery questions:

- parent/root record through `MainTable`;
- child collections through `MainTable.ChildTable`;
- child-control mapping through `DataSourceName`;
- parent-key propagation through `MainTable.PkName`;
- child insert preparation excludes the child primary-key field so the host can provide/generated it;
- required/unique host validation;
- `CheckBeforeSave` veto;
- `SaveRecord` host save boundary;
- `PerformAfterSave(ID)` generated-record identity callback;
- New/Edit/None lifecycle and reload behavior;
- host permission/logging flags as host behavior rather than Hive authorization.

### 20.1 HDataGridView and AddGrid production evidence

The supplied `HDataGridView` and `AddGrid` source now establishes the concrete V1 child-grid interaction lifecycle:

- `HDataGridView` uses its bound `Dt` as the editable data surface and `ReadDataTable()` binds that `DataTable` directly to the underlying `DataGridView`.
- `CellEndEdit` calls `Validate()` so edited cell values are committed back to the bound data surface before the surrounding HDataBox save lifecycle serializes the child rows.
- The grid's add/edit button handlers are active only for `GridEditMode.ByForm` and only while the surrounding HDataBox is in New or Edit mode.
- Before add/edit, the host can veto through `CheckBeforeAddGrid` / `CheckBeforeEditGrid`; before delete, through `CheckBeforeDeleteGrid`.
- `ByForm` opens the configured `GridDialog`, linking the dialog back to the owning grid through `RelatedHDGV` and selecting Add/Edit dialog mode.
- `AddGrid` performs host-level required/repeat validation through `CheckRequiredData()` and `CheckRepeatData()` before applying add/edit data.
- After the dialog applies the data, `RelatedHDGV.PerformGridDataChanged(...)` refreshes the grid and the dialog exposes post-add/post-edit/post-save extension points.
- Grid delete ends the current edit, removes the selected row from `Dt`, and then raises the grid-data-changed/after-delete hooks. This is an in-memory child-surface mutation; parent HDataBox persistence remains responsible for the eventual business write.

This confirms that HForms `ByForm` is not merely descriptive metadata: it is a concrete input-dialog workflow around the grid's bound data surface.

`HList.ReadDataTable()` also establishes a distinct list-selection pattern: it reads the child `DataTable`'s configured `DbFieldName` and checks matching list-item IDs. This is selection/state synchronization, not evidence that every HList is a CRUD data-entry surface.
Before freezing the concrete Phase 1.14 adapter types, the remaining implementation-specific HForms/HControls contracts to inspect are:

- exact `HControl`/`IHyperControl` semantic metadata and value access needed by the neutral descriptor;
- exact `HDataGridView` column metadata plus the existing row identity/key field representation used by the host when locating persisted child rows; positional row index remains non-authoritative;
- exact generated/computed-column behavior and edit serialization for existing child rows where the supplied snippets do not expose the complete `PerformEditData` implementation;
- exact lookup resolution behavior;
- exact concurrency/version behavior where the host exposes it;
- the concrete `HActionBar` contract only where the adapter needs to expose or invoke its actions; it is still unfinished and is not treated as authoritative V1 evidence today.

Do not recreate these mechanisms in Hive when the host already exposes an authoritative contract.

The goal is to adapt existing host semantics into Hive's neutral boundary, not to recreate the host's data-access or business framework.
