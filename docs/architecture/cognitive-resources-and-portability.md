# Hive Architecture — Cognitive Resources and Configuration Portability



This document is part of the authoritative architecture defined by `docs/architecture.md`. It contains the detailed resource families and future configuration portability boundary.



## 11. Cognitive Resources

Advanced persistent cognitive resources are delivered after the cognitive lifecycle branch exists. Persistent resources must preserve the distinction between actual experience and simulated/dreamed outcomes and must preserve the evidence that led to adaptation.

They include:

- working / episodic / semantic / procedural memory families;
- Knowledge and managed Wiki;
- versioned Skills;
- Learning Candidates and governed promotion;
- applicability/reliability state;
- resource assignments and runtime overrides.

### 11.1 Cognitive evidence

CognitiveAgent evidence distinguishes at least:

```
Actual
    observed/executed in the real environment

Simulated
    produced by Dream, counterfactual analysis, or other bounded prediction

Human-corrected
    explicitly supplied or corrected by an authorized human

External
    supplied by an attributable external source
```

An Experience describes what actually happened. An OutcomeEvaluation is a first-class cognitive evaluation that interprets that experience against the relevant objective or success criteria and establishes a provenance-bearing outcome classification. Mistake and Success are first-class outcome concepts associated with that evaluation, not raw execution states; dedicated processors or persistence representations may be introduced when their lifecycle or ownership requires them.

OutcomeEvaluation classifies a result into one mutually exclusive outcome for that evaluation:

- Success — all applicable success criteria were satisfied;
- Partial — some but not all applicable criteria were satisfied and the result is incomplete rather than wholly incorrect;
- Mistake — the result is known to be wrong relative to the intended objective or success criteria and is not better classified as Partial;
- Unknown / unresolved — available evidence cannot establish the substantive outcome.

The evaluation should preserve:

- expected result/success criteria;
- observed actual result;
- evidence supporting the evaluation;
- separate assessment of outcome correctness versus method/strategy quality;
- Objective/Goal/Intention/Plan/Method/Decision provenance;
- attribution/credit context;
- confidence and uncertainty;
- relevant cost, time, risk, and consequence data;
- applicability conditions.

Mistake and Success are therefore meaningful learning evidence, but the system does not equate them with raw execution status. A Success identifies that the applicable objective was achieved, not that the method was inherently good or reusable; a Mistake identifies that the intended result was wrong, not that the Agent caused it. Attribution and method-quality assessment remain separate evidence problems.

### 11.2 Learning from outcomes

The learning path is:

```
actual experience
      ↓
outcome evaluation
      ↓
Mistake / Success / Partial / Unknown
      ↓
interpretation and attribution
      ↓
Learning Candidate
      ↓
validation / governance
      ↓
promoted cognitive resource or strategy adaptation
```

Both positive and negative evidence can produce Learning Candidates.

Examples include:

- repeated evaluated Success supporting a reusable method under stated conditions;
- repeated evaluated Mistake outcomes supporting a changed validation rule or decomposition strategy;
- mixed outcomes narrowing the applicability conditions of a method;
- human correction identifying an incorrect assumption;
- Dream evidence suggesting an alternative method or exposing a hidden failure condition;
- repeated evidence showing that a deterministic procedure can replace an unnecessary model call.

A candidate must preserve its evidence, applicability, and intended adaptation target rather than storing only a sentence such as "this works." Where relevant, it also links to the task-scoped attempts/candidates that produced the evidence, the materially distinct methods tried, the observed basis for stagnation or method switching, applicable critique/check results, and the conditions under which each method did or did not work. It must distinguish repeated trials of one method from genuinely independent approaches and distinguish model self-assessment from external or deterministic verification.

A Learning Candidate may propose that a method should be preferred, avoided, refined, or checked more heavily under stated conditions. A single successful output or model assertion does not establish general reliability. Candidate promotion remains governed, versioned, scoped, revocable, and subject to later contradictory evidence; simulated/Dream results can inform a candidate but never become actual experience.

### 11.3 Dream evidence and Nightmares

Dream evidence may support Learning Candidates but never becomes actual Experience.

A Recovery Dream explores alternatives after a Mistake.

An Optimization Dream searches for a better way to reproduce a Success.

A Nightmare/Stress-Test Dream attempts to falsify or weaken an apparently successful method by exploring adverse conditions.

A Nightmare that discovers a plausible failure condition is not itself a real failure. It is simulated evidence that may produce an applicability boundary, safeguard, Question, or later Learning Candidate.

### 11.4 Risk, Fear, and Confidence state

Risk, Fear, and Confidence are first-class CognitiveAgent state semantics. They belong to cognitive state/strategy rather than generic configuration resources, but they may have dedicated persistence records, event streams, projections, or processors when their lifecycle or update boundary requires one.

They should be contextual, versioned, and attributable to supporting evidence. They may influence decomposition, verification, specialist escalation, Question generation, Dream selection, and deterministic-vs-model-assisted routing.

They must never be used as authorization state or as a substitute for explicit policy/capability checks.

Learning is a first-class governed cognitive process, not model-weight training. Model output produces evidence or a candidate; authoritative cognitive/resource state changes only through Hive's validation, policy, authorization, reconciliation, and promotion boundaries. Promoted deterministic shortcuts must remain attributable, scoped by applicability conditions, and revocable/revisable when later evidence invalidates them.

### 11.5 Persistence backend and vector representation independence

Cognitive resources and the durable evidence/state that supports them are logical Hive resources, not SQL Server-specific or Embedded-specific resource types. The selected persistence backend stores the same authoritative resource/state contracts and preserves the same ownership, scope, provenance, lifecycle, and version semantics.

A future vector representation used for retrieval is derived data. It is not the authoritative representation of a cognitive resource, WorkItem, evidence record, or configuration record, and it must be rebuildable from authoritative persisted state under both the SQL Server and Embedded profiles. Vector retrieval is deferred (see the [Deferred Vector Retrieval assessment](../plan/Deferred/Vector-Retrieval.md)): no `IVectorStore` contract, SQL Server native vector storage, or embedded index library is selected or authorized, and any such choice is made only if Phase 5.1 identifies a concrete retrieval need.

Changing persistence backend does not change the meaning of a cognitive resource. The direct SQL Server ↔ Embedded Hive-data migration boundary is established by 1.18A; broader configuration/resource portability remains a later concern. Neither migration nor synchronization may be implemented as a semantic-learning mechanism.

### 11.6 Agent-level experience, memory ownership, and knowledge exchange

Hive distinguishes four information boundaries that serve different purposes:

- **Runtime working state** — context and records tied to a particular RuntimeInstance or execution, useful for active work and recovery.
- **Persistent Agent experience** — attributable history that belongs to the stable Agent identity across runtime incarnations, including actual observations, evaluated outcomes, corrections, methods tried, and their applicability conditions.
- **Hive-shared problem evidence** — findings, objections, candidate answers, and checks deliberately contributed to one collaborative goal.
- **Reusable expertise** — governed Knowledge and versioned Skills or adaptations that can be discovered by other authorized Agents/Hives.

These are logical ownership distinctions, not a mandate to create four parallel storage engines or make every concept a generic Resource. Reuse the established persistence/event/snapshot contracts and introduce a dedicated lifecycle boundary only when its semantics require one.

The current Base-Agent `IAgentMemoryStore` is RuntimeInstance-scoped. Its durable persistence supports existing runtime work-state semantics, but it is not by itself the future Agent-level experience repository described above. A later Agent-level experience contract must preserve stable Agent ownership while retaining origin RuntimeId, owning task/WorkItem or conversation correlation where applicable, source/execution references, time, provenance, and actual/simulated/human-corrected/external evidence classification. Runtime reincarnation must not create a false second experience or erase the original context.

Memory storage is not learning. A stored record can affect a future result only if Hive retrieves relevant information, supplies it to an appropriate decision or method, and evaluates what happened. Adaptive learning additionally requires outcome interpretation, attribution where possible, a proposed change, validation, versioned promotion, and later evaluation of that change. This is not model-weight training, and no claim that memory volume alone improves intelligence is permitted.

An Agent may offer an explicit contribution to its parent or another authorized recipient: selected source experiences, an evaluation, a candidate lesson, a procedure, or evidence needed to assess a claim. A parent may also pull relevant records when authorized, including more detailed source records when a summary is inadequate. The recipient independently evaluates what the evidence supports before incorporating it into its own memory, cognitive state, or a reusable resource. Acceptance by one Agent does not automatically certify a claim for all Agents.

Do not bulk-copy a child's entire private memory into its parent by default. Keep source records attributable, preserve applicability limits and contradictions, and distinguish the recipient's own assessment from the source Agent's conclusion. Parent lineage does not grant data access: scope, permissions, privacy, and any host/tenant restrictions remain authoritative. This controlled exchange is available to authorized Agents generally; it is not Hive-to-Hive delegation.

A dormant parent may request or retrieve selected child contributions without requiring every child runtime to be active, provided the source records are durable and the access is authorized. Any resulting adaptation still passes through the recipient's normal validation and governance boundary.

---



## 12. Configuration Portability

Configuration portability is a later platform capability.

Packages serialize Hive's authoritative configuration contracts rather than creating a second model.

Possible contents include:

- Providers / ProviderAccounts;
- Execution Targets;
- Agents / Hives;
- Skills;
- Knowledge / Wiki;
- Memory configuration;
- Learning configuration;
- Persistence configuration (with credential material handled through the Secret Store/export policy rather than a second persistence-configuration model);
- Tools;
- permissions/policies;
- runtime defaults.

Configuration packages and portable specialist packages are related but distinct. A configuration package primarily carries definitions and settings. A future **portable specialist package** may additionally include selected Agent experience and approved cognitive resources for which export is authorized. It should preserve the specialty profile, configuration lineage, package/source identity, relevant Experience/OutcomeEvaluation provenance, applicability conditions, approved Skills/Knowledge, and compatibility versions. It must not imply that all private memory is exportable or that the destination has the same operating conditions.

Live executions, live runtime handles, synchronization primitives, transient process state, arbitrary host objects, opaque executable handlers, credentials, and authorization grants are excluded. A selected experience payload may refer to originating runtimes/executions as provenance but does not restore them as live resources. Export of private history must be explicit and scoped; it is not the default for ordinary configuration export.

Import creates or maps to local resource identities and resolves target-specific dependencies through the destination platform's authorized Management boundary. Preserve origin/package lineage without blindly copying credentials, execution-target identities, host integration bindings, or permission grants. The destination must validate compatibility and applicability before using imported experience or skills.

Credentials are excluded by default.

---
