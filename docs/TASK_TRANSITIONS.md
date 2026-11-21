# WorkFlowPro – Task Status Transition Policy

This document defines the formal state transition matrix for WorkFlowPro tasks (FR-03).
Transitions outside this matrix violate domain invariants and are rejected with HTTP 400 Problem Details.

## 1. Supported Statuses

| Status | Meaning |
|---|---|
| `Backlog` | Item recorded in the backlog, not yet committed to an active iteration or immediate work. |
| `ToDo` | Item prioritized and ready to be picked up by an assignee. |
| `InProgress` | Work is actively underway. |
| `Blocked` | Work has stopped due to an external dependency or impediment. |
| `Done` | Work is completed and accepted. |
| `Cancelled` | Work was abandoned, closed as obsolete, or rejected. |

## 2. Transition Matrix

| From Status \ To Status | Backlog | ToDo | InProgress | Blocked | Done | Cancelled |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Backlog** | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ |
| **ToDo** | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ |
| **InProgress** | ❌ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Blocked** | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ |
| **Done** | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Cancelled** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |

### Notes:
- Self-transitions (e.g. `InProgress` ➔ `InProgress`) are rejected as redundant operations.
- Reopening completed work (`Done`) can move to either `InProgress` (resuming immediate work) or `ToDo` (re-queued for future iteration).
- Reviving cancelled tasks (`Cancelled`) returns them to `Backlog` or `ToDo`.
- Every transition automatically creates an immutable audit entry in `task_status_histories`.
