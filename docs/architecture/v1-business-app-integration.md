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

Durable business-operation receipts and first-class Review records are owned by Phase 1.17. Their semantics are defined in Sections 12–13 so the earlier host-integration contracts do not have to be redesigned later.

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

An operation that begins from a row index resolves the selected row through the host's existing row/ID behavior before a consequential operation is committed. The inspected applications normally do not change an existing primary-key ID. A DataGridView editing path may delete and reinsert a saved row internally, but the existing host mechanism already handles that case through row-index-based editing and it is not a separate Hive identity requirement.

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

Generated fields exist in the inspected applications, but their implementation mechanism is not a contract requirement. Generation may come from the database, host code, computed properties, or another host-specific mechanism.

Hive therefore treats generation semantically:

- whether the field is generated;
- whether the host supplies the resulting value;
- whether the field may be supplied by the caller before the operation;
- what resulting value/identity the host returns after create or save.

Hive must not invent generated values merely because an add-row operation requires the field.

### 6.4 Computed fields

Computed fields also exist in the inspected applications. Their implementation mechanism is host-defined and may take many forms.

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

Phase 1.14 is intentionally contract-first. Hive owns the neutral public integration contracts; the host application implements those contracts against its own controls, data surfaces, business objects, and application services.

The intended ownership model is:

```text
Hive.Core
  ↓
Hive-owned host integration contracts

Host application
  ↓
implements those contracts against its own types

Hive host integration/runtime
  ↓
discovers, composes, governs, and invokes the implemented capabilities
```

A host may implement a Hive contract directly on an application-owned type where that is the simplest design. For example, a custom text control can implement the semantic field/value contract, while a host Form can implement the host-context contract. The exact interface/type names are part of Phase 1.14 contract design and are not prescribed by this document.

### 17.1 Minimize host implementation work

The host integration should do as much reusable work as possible inside Hive.

Hive should provide reusable infrastructure for:

- bounded Form/control traversal and host-context capture;
- common WinForms control/value adaptation where the host's semantics are standard;
- common metadata projection;
- row/field/lookup capability plumbing;
- capability discovery and bounded invocation;
- cancellation, lifecycle, provenance, and authorization integration;
- contract-level validation and deterministic failure handling;
- reusable contract tests that a host implementation can run against its own adapter.

The host application should provide only the application-specific semantic information and behaviors that Hive cannot safely infer, such as:

- which controls/data surfaces are meaningful to Hive;
- application-specific field semantics;
- authoritative parent/child relationships;
- lookup resolution that depends on application context;
- business/application actions and their concrete implementation;
- host-specific validation or save semantics.

The goal is that a host developer implements a small, explicit contract surface while Hive handles the reusable discovery, orchestration, governance, and safety work.

### 17.2 Host context

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

Hive may then discover the registered surface and use the semantic contracts directly. Raw WinForms controls, arbitrary reflection, SQL, credentials, and unrestricted method invocation remain outside the neutral public contract.

### 17.3 Public boundary

Public Hive documentation describes the neutral contracts, guarantees, lifecycle, security boundaries, and supported semantics. A host application's private classes, methods, source code, database schema, control library, query text, and private business conventions remain outside the Hive repository's architectural contract.

A host may publish its own adapter implementation separately. Hive only depends on conformance to the Hive contract.

### 17.4 Remaining contract-definition work

The remaining Phase 1.14 design work is limited to:

- exact neutral public type shapes for host, control, field, row, data surface, lookup, and action contracts;
- reusable default/base implementations for common host patterns where they reduce host-side code without hiding meaningful application semantics;
- exact context/discovery-to-action transition and capability authorization boundary;
- bounded lookup request/result context;
- generated/computed field representation and resulting values;
- stable primary-key ID row mapping and child-row mutation semantics;
- concrete cancellation, lifecycle, provenance, and disposal contracts;
- the contract-test/reference-fixture strategy.

These are contract-definition questions, not a request to document or support every possible host implementation.

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

- exact neutral public type shapes and bounded operation semantics;
- adapter mapping from the established binding/value model to the neutral contracts;
- bounded lookup request/result mapping, including dependent lookup context;
- representation of generated/computed semantics and resulting values;
- mapping of the established host action surface into neutral capabilities;
- optional concurrency evidence only where the host exposes it.

Do not recreate these host mechanisms in Hive when the host already exposes an authoritative contract.

The goal is to adapt existing host semantics into Hive's neutral boundary, not to recreate the host's data-access or business framework.
