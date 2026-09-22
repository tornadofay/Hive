# Hive Architecture — Cognitive Resources and Configuration Portability



This document is part of the authoritative architecture defined by `docs/architecture.md`. It contains the detailed resource families and future configuration portability boundary.



## 11. Cognitive Resources

Advanced persistent cognitive resources are delivered after the cognitive lifecycle branch exists. Persistent resources must preserve the distinction between actual experience and simulated/dreamed outcomes.

They include:

- working / episodic / semantic / procedural memory families;
- Knowledge and managed Wiki;
- versioned Skills;
- Learning Candidates and governed promotion;
- applicability/reliability state;
- resource assignments and runtime overrides.

Learning is not model-weight training. Model output produces evidence or a candidate; authoritative resource state changes only through Hive's validation, policy, authorization, and promotion boundaries.

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