# Hive — Active Work

Status: ACTIVE TASK

**Authorized slice:** Phase 1.20 / Slice 2 — Chat UX redesign.

Authorized by `Hive: Start next slice` on 2026-10-23. Phase 1.20 / Slice 1 (Workspace Foundation) is closed; this is the next slice within the already authorized Phase 1.20.

**Bounded task:** redesign the Direct LLM conversation presentation inside the Workspace shell: modern conversation layout and message presentation. Existing conversation/history/execution behavior remains unchanged; presentation-only unless a change is strictly required by the agreed layout. No Agent mode (Phase 1.21), no runtime context/WorkItem visibility (Slice 3), and no restart/reload handling (Slice 4).

**Layout:** agreed with developer on 2026-10-23: modern chat bubbles with a conversation history sidebar (messenger style). Implementation in progress.

**Status:** remediation complete; awaiting developer verification.

**Verification gate:** VERIFICATION PENDING. Required checks:
- Build `Hive.Host.WinForms` (done here: succeeded, 0 warnings/errors).
- Run `HiveWorkspaceLifecycleTests`.
- Run `DirectLlmConversationTests` (backend, unchanged).
- Manual smoke: open `Hive.Example.WinForms` → `Overview / Getting Started / Workspace`, send messages, switch conversations, toggle theme; confirm reduced spacing and no excessive empty scroll area.

**Planned follow-up (not authorized):** after this Slice 2 verification closes, the next UI work should combine the agreed Workspace design direction with the deep UI audit findings. Direction: compact IDE-style panel organization (left navigation/history, central conversation, collapsible right context) with clean modern card-based content only where grouping adds meaning; restrained borders, subtle depth, consistent typography/spacing, polished icons, and excellent light/dark themes. This is not a generic card dashboard and not a code-editor skin. Candidate follow-up slices: (a) token/radius/typography/spacing normalization plus reusable card and empty-state surfaces; (b) Workspace shell polish for history card, conversation card, composer, and collapsible right context; (c) CRUD/settings/form consistency and DPI-safe layout. This note records planning only and does not authorize implementation or change this slice's verification gate.
