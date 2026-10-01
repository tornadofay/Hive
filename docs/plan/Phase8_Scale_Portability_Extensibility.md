# Phase 8 — Multi-Tenancy, Scale, Configuration Portability & Extensibility

This document contains the detailed ordered plan for this phase. It does not authorize implementation; authorization remains in `docs/Hive_Active_Work.md`.

## 8.1 — Authentication Boundary
Add real authentication integration.

## 8.2 — Distributed Execution Decision Point
Re-evaluate Temporal/Dapr/distributed execution only from measured operational requirements.

## 8.3 — Configuration Import/Export
Versioned Hive configuration packages with compatibility/conflict handling.

## 8.4 — MCP / Tool Extensibility
Additional governed tool-extension boundary.

## 8.5 — Additional Host Surfaces
WPF/web/other hosts consume the same core and Management contracts.

## 8.6 — Lightweight / Embedded Hive Deployment Profile
Objective: provide an optional local/embedded persistence deployment for users who should not need to install or operate a separate SQL Server instance, while preserving the same Hive resource model, Management contracts, event/snapshot/outbox semantics, and authorization boundaries.

Scope:
- select and document a mature embedded persistence technology rather than creating a database engine without a measured requirement;
- reuse the existing Hive persistence/resource contracts instead of maintaining a second logical schema/model;
- define which capabilities the embedded backend supports, including vector storage/search;
- keep SQL Server as the server-oriented V1 persistence implementation;
- make backend selection explicit and configuration-driven;
- preserve migration/version/concurrency/security semantics across supported backends;
- provide a clear upgrade/export path from local/embedded deployment to the server-oriented persistence profile when required.

This slice is a deployment/storage portability capability, not permission to fork Hive's domain model or introduce a separate vector database.

Verify: clean local install, restart/persistence durability, migrations/upgrades, concurrency, crash/recovery, secret handling, supported vector-search behavior where available, explicit unsupported-capability reporting, and configuration migration between supported deployment profiles where that contract is provided.

---
