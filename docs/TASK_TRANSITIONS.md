# Jiva – Task Status Transition Policy

This document defines the formal state transition matrix for Jiva tasks (FR-03).
Transitions outside this matrix violate domain invariants and are rejected with HTTP 400 Problem Details.

## 1. Supported Statuses

| Status | Meaning |
|---|---|
| `Backlog` | Item recorded in the backlog, not yet committed to an active iteration or immediate work. |
| `ToDo` | Item prioritized and ready to be picked up by an assignee. |
| `InProgress` | Work is actively underway (auto-triggered when a branch is created or a Draft PR opens). |
| `InReview` | A non-draft Pull Request is open and awaiting code review / CI checks. |
| `Blocked` | Work has stopped due to an external dependency or impediment. |
| `Done` | Work is completed and accepted (auto-triggered when a linked PR is merged). |
| `Cancelled` | Work was abandoned, closed as obsolete, or rejected. |

## 2. Transition Matrix

| From Status \ To Status | Backlog | ToDo | InProgress | InReview | Blocked | Done | Cancelled |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Backlog** | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| **ToDo** | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ |
| **InProgress** | ❌ | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ |
| **InReview** | ❌ | ❌ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Blocked** | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ |
| **Done** | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Cancelled** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |

### Notes:
- Self-transitions (e.g. `InProgress` ➔ `InProgress`) are rejected as redundant operations.
- Reopening completed work (`Done`) can move to either `InProgress` (resuming immediate work) or `ToDo` (re-queued for future iteration).
- Reviving cancelled tasks (`Cancelled`) returns them to `Backlog` or `ToDo`.
- **Automated Git/PR Transitions**: When Git events advance a card across stages (e.g., `Backlog` ➔ `InReview` on PR open, or `ToDo` ➔ `Done` on PR merge), `TaskItem.ApplyAutomationTransition` walks the shortest valid path in this matrix and records each step in `task_status_histories`.
- Every transition automatically creates an immutable audit entry in `task_status_histories`.
