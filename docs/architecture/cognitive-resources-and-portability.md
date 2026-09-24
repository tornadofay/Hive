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

An Experience describes what actually happened. An OutcomeEvaluation is a first-class cognitive evaluation that interprets that experience against the relevant objective or success criteria. Mistake and Success are first-class outcome interpretations produced by that boundary, not raw execution states.

OutcomeEvaluation may classify the result as:

- Success;
- Mistake;
- Partial;
- Unknown / unresolved.

The evaluation should preserve:

- expected result/success criteria;
- observed actual result;
- evidence supporting the evaluation;
- Objective/Goal/Intention/Plan/Method/Decision provenance;
- attribution/credit context;
- confidence and uncertainty;
- relevant cost, time, risk, and consequence data;
- applicability conditions.

Mistake and Success are therefore meaningful learning evidence, but the system does not equate them with raw execution status. Attribution remains separate: a Mistake identifies that the intended result was not achieved, while causal analysis determines whether the failure arose from the Agent's reasoning, a tool, a specialist, the environment, missing information, or another factor.

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

- repeated Success supporting a reusable method under stated conditions;
- repeated Mistakes supporting a changed validation rule or decomposition strategy;
- mixed outcomes narrowing the applicability conditions of a method;
- human correction identifying an incorrect assumption;
- Dream evidence suggesting an alternative method or exposing a hidden failure condition;
- repeated evidence showing that a deterministic procedure can replace an unnecessary model call.

A candidate must preserve its evidence and applicability rather than storing only a sentence such as "this works."

### 11.3 Dream evidence and Nightmares

Dream evidence may support Learning Candidates but never becomes actual Experience.

A Recovery Dream explores alternatives after a Mistake.

An Optimization Dream searches for a better way to reproduce a Success.

A Nightmare/Stress-Test Dream attempts to falsify or weaken an apparently successful method by exploring adverse conditions.

A successful Nightmare is not itself a real failure. It is simulated evidence that may produce an applicability boundary, safeguard, Question, or new candidate lesson.

### 11.4 Risk, Fear, and Confidence state

Risk, Fear, and Confidence are first-class CognitiveAgent state semantics. They belong to cognitive state/strategy rather than generic configuration resources, but they may have dedicated persistence records, event streams, projections, or processors when their lifecycle or update boundary requires one.

They should be contextual, versioned, and attributable to supporting evidence. They may influence decomposition, verification, specialist escalation, Question generation, Dream selection, and deterministic-vs-model-assisted routing.

They must never be used as authorization state or as a substitute for explicit policy/capability checks.

Learning is a first-class governed cognitive process, not model-weight training. Model output produces evidence or a candidate; authoritative cognitive/resource state changes only through Hive's validation, policy, authorization, reconciliation, and promotion boundaries. Promoted deterministic shortcuts must remain attributable, scoped by applicability conditions, and revocable/revisable when later evidence invalidates them.

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

Live executions, synchronization primitives, transient process state, arbitrary host objects, and executable handlers are excluded.

Credentials are excluded by default.

---