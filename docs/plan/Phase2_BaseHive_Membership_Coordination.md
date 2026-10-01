# Phase 2 — Base Hive Membership & Coordination

This document contains the detailed ordered plan for this phase. It does not authorize implementation; authorization remains in `docs/Hive_Active_Work.md`.

## 2.1 — HiveDefinition & Membership
Define Hive identity, membership, roles, and membership lifecycle.

## 2.2 — Configurable Agent Composition
Add/remove/reorder Agents through Hive.Management without hard-coded application wiring.

## 2.3 — Hive ↔ Agent Communication
Use explicit message/DTO contracts with correlation/provenance.

## 2.4 — Shared Claims with Provenance
Provide governed shared claims without making the shared store authoritative over host domain state.

## 2.5 — Supervisor Controls
Observe/pause/stop members through Hive/MAF-supported mechanisms.

## 2.6 — Agent-Owned Hive Creation & Hive Lifecycle
Allow an Agent to explicitly create/sponsor a persistent Hive for a bounded need without changing the Agent's own type. Sponsorship is a relationship, not implicit lifecycle ownership; sponsor death/retirement/deletion does not automatically delete the Hive or its members.

## 2.7 — Specialty-Driven Population & Swarm Participation
Allow an authorized Hive to create or reuse Agents of any supported generation for missing specialties, including CognitiveAgents when the Hive's population policy explicitly permits that generation.

Define Swarm as the active subset of Hive members collaborating on a WorkItem, Question, or bounded problem. Swarm is derived/session state, not a persistent resource. A member Agent normally requests missing specialists through the parent Hive rather than recursively creating a child Hive.

All coordination uses MAF orchestration primitives where applicable; Hive does not become a second workflow engine.

## 2.8 — Hive Coordination Workspace & Swarm Extensions
Objective: extend the V1 Workspace with persistent Hive organization and collective coordination capabilities.

Scope:
- Hive organization/topology;
- Hive membership presentation;
- persistent Hive/Agent relationships;
- active Swarm visibility;
- Hive-level coordination;
- collective work context;
- Workspace representation of persistent Hive membership;
- Agentic behavior that specifically depends on Hive membership or Swarm state.

Basic LLM mode, basic Agent mode, ordinary multi-Agent application work, and single-application Agent assignment remain V1 capabilities.
