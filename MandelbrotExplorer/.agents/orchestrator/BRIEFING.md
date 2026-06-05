# BRIEFING — 2026-06-05T19:21:35+02:00

## Mission
Refactor the Fractal Explorer codebase to split the monolithic `MainViewModel` into cohesive sub-ViewModels (NavigationViewModel, DiagnosticsViewModel, RenderingViewModel), and integrate Microsoft.Extensions.Logging, while maintaining all functionality and passing tests.

## 🔒 My Identity
- Archetype: teamwork_preview_orchestrator
- Roles: orchestrator, user_liaison, human_reporter, successor
- Working directory: c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\.agents\orchestrator\
- Original parent: Sentinel
- Original parent conversation ID: 0465ae12-86a0-4fae-8dcd-93afd29679ed

## 🔒 My Workflow
- **Pattern**: Project
- **Scope document**: PROJECT.md
1. **Decompose**: Decompose requirements into milestones (e.g. analysis, sub-ViewModels split, logging integration, E2E tests and validation) and record them in PROJECT.md.
2. **Dispatch & Execute**:
   - **Direct (iteration loop)**: Explorer → Worker → Reviewer → test → gate
   - **Delegate (sub-orchestrator)**: Spawn subagents for specific milestones when needed.
3. **On failure** (in this order):
   - Retry: nudge stuck agent or re-send task
   - Replace: spawn fresh agent with partial progress
   - Skip: proceed without (only if non-critical)
   - Redistribute: split stuck agent's remaining work
   - Redesign: re-partition decomposition
   - Escalate: report to parent (sub-orchestrators only, last resort)
4. **Succession**: Self-succeed at 16 spawns, write handoff.md, spawn successor.
- **Work items**:
  1. Initial exploration and planning [done]
  2. Sub-ViewModels implementation and integration [pending]
  3. Logging integration [pending]
  4. Dual track test suite setup [in-progress]
  5. Verification and validation [pending]
- **Current phase**: 1
- **Current focus**: Dual track test suite setup (E2E testing track)

## 🔒 Key Constraints
- NEVER write, modify, or create source code files directly.
- NEVER run build/test commands yourself — require workers to do so.
- Integrity mode: development.
- Maximum agent limit: 128.
- Succession threshold: 16 spawns.
- Never reuse a subagent after it has delivered its handoff.

## Current Parent
- Conversation ID: 0465ae12-86a0-4fae-8dcd-93afd29679ed
- Updated: not yet

## Key Decisions Made
- Incorporate R4 (Floating Cancel Render Option) into the project design and coordinate it across both parallel tracks.

## Team Roster
| Agent | Type | Work Item | Status | Conv ID |
|-------|------|-----------|--------|---------|
| aa7abed5 | teamwork_preview_explorer | Initial exploration and baseline tests | completed | aa7abed5-0266-44b9-81f6-279c2e156c87 |
| e04a1f4a | self (sub_orch) | E2E Testing Track | in-progress | e04a1f4a-5089-4ab6-8328-e8e75fe1c154 |
| 4bc7da54 | self (sub_orch) | Implementation Track | in-progress | 4bc7da54-731e-4cc0-b64d-b9f0a4889c95 |

## Succession Status
- Succession required: no
- Spawn count: 3 / 16
- Pending subagents: e04a1f4a-5089-4ab6-8328-e8e75fe1c154, 4bc7da54-731e-4cc0-b64d-b9f0a4889c95
- Predecessor: none
- Successor: not yet spawned

## Active Timers
- Heartbeat cron: task-29
- Safety timer: none
- On succession: kill all timers before spawning successor
- On context truncation: run manage_task(Action="list") — re-create if missing

## Artifact Index
- c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\.agents\orchestrator\BRIEFING.md — Persistent memory and index.
