# Approval Workflows Architecture & Specification

## 1. Overview

WorkFlowPro provides a sequential, multi-step approval workflow engine designed for organizational controls such as purchase orders, travel authorizations, capital expenditure, and system access requests.

### Core Architecture Principles
- **Sequential Execution**: Steps progress in strict ascending order (1, 2, ..., N).
- **Snapshot at Submission**: When an approval request is created, the workflow definition's steps and resolved concrete approvers are frozen in `approval_request_steps`. Subsequent alterations to the workflow definition do not mutate in-flight requests.
- **Strict Role-to-User Resolution Policy**:
  - Each step in a workflow definition specifies either a concrete user (`ApproverUserId`) OR an organizational role (`ApproverRole`).
  - At submission time:
    - If `ApproverUserId` is configured, that user is designated directly.
    - If `ApproverRole` is configured, the requester may optionally provide a `StepApproverOverride` selecting an eligible active member who possesses that role.
    - If no override is provided, the engine queries active users in that role and deterministically selects the user with the earliest alphabetical `UserName` (with ties broken by `Id`).
    - If no active users belong to the required role, submission fails with HTTP 400 Bad Request.
- **Designated Approver Enforcement**: Only the concrete user designated on the currently active step (`CurrentStepOrder`) can approve or reject that step. Any attempt by other users (including requesters or other approvers) returns HTTP 403 Forbidden.
- **Optimistic Concurrency Control**: Approval requests track an integer `Version` concurrency token. Decisions must include `expectedVersion`. Conflicting simultaneous updates result in HTTP 409 Conflict.
- **Terminal Transitions**:
  - **Rejection**: If an approver rejects a step, that step is marked `Rejected`, all remaining future steps are marked `Skipped`, and the request immediately terminates as `Rejected`.
  - **Completion**: Once all sequential steps are marked `Approved`, the request immediately transitions to `Approved` with `CompletedAtUtc` recorded.
  - **Cancellation**: A requester or administrator can cancel a `Pending` request, which transitions it to `Cancelled` and marks remaining steps `Skipped`.

---

## 2. Status Matrices

### Request Statuses (`ApprovalRequestStatuses`)
- `Pending`: In progress, awaiting decision on `CurrentStepOrder`.
- `Approved`: All sequential steps successfully approved.
- `Rejected`: Rejected by an assigned approver; execution terminated.
- `Cancelled`: Terminated early by the requester or administrator.

### Step Statuses (`ApprovalStepStatuses`)
- `Pending`: Awaiting decision by assigned `ApproverUserId`.
- `Approved`: Positively approved by assigned `ApproverUserId`.
- `Rejected`: Rejected by assigned `ApproverUserId`.
- `Skipped`: Bypassed because a prior step was rejected or the request was cancelled.

---

## 3. Concurrency & Database Invariants

1. `unique(workflow_definition_id, step_order)`: Guarantees no two steps in a definition share the same order.
2. `unique(approval_request_id, step_order)`: Guarantees unique step sequence per request.
3. `unique(approval_request_step_id)` on `approval_decisions`: Guarantees at the database level that a step cannot have duplicate decision records.
4. `RequestNumber`: Sequential human-friendly number (`#1001+`), unique indexed.
5. `Version`: Incrementing concurrency token checked before state transitions to prevent race conditions.
