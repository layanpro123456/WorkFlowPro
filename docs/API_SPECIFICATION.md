# Jiva – API Specification

**Base path:** `/api/v1`  
**Format:** JSON  
**Authentication:** Bearer access token unless stated otherwise  
**Status:** Implemented contract (verify against `/scalar/v1` OpenAPI output in Development).

## 1. Conventions

- IDs are UUID strings.
- Timestamps are ISO 8601 UTC.
- Lists use `page` and `pageSize`; maximum pageSize 100.
- Errors use `application/problem+json`.
- Never return EF entities directly.
- Authorization applies to individual resources, not just endpoint roles.
- Use idempotency only where explicitly designed; do not claim all POST calls are idempotent.

## 2. Authentication

| Method | Route | Purpose |
|---|---|---|
| POST | `/auth/login` | Authenticate and issue access token / refresh cookie |
| POST | `/auth/refresh` | Rotate refresh token |
| POST | `/auth/logout` | Revoke refresh token |
| GET | `/auth/me` | Current user profile and roles |

Example login request:
```json
{
  "email": "demo@example.com",
  "password": "provided-through-local-seed-only"
}
```

Response example (illustrative contract):
```json
{
  "accessToken": "<short-lived-token>",
  "expiresAt": "2026-01-01T10:15:00Z",
  "user": {
    "id": "00000000-0000-0000-0000-000000000001",
    "displayName": "Demo User",
    "roles": ["Employee"]
  }
}
```
Refresh token should be delivered through a Secure, HttpOnly, appropriately SameSite cookie for browser use, not exposed as a long-lived token in JSON.

## 3. Projects

| Method | Route | Purpose |
|---|---|---|
| GET | `/projects` | List projects visible to caller |
| POST | `/projects` | Create project |
| GET | `/projects/{projectId}` | Project details |
| PUT | `/projects/{projectId}` | Update project |
| POST | `/projects/{projectId}/archive` | Archive project |
| GET | `/projects/{projectId}/members` | List members |
| POST | `/projects/{projectId}/members` | Add member |
| DELETE | `/projects/{projectId}/members/{userId}` | Remove member |

List filters: `status`, `search`, `page`, `pageSize`.

## 4. Tasks

| Method | Route | Purpose |
|---|---|---|
| GET | `/projects/{projectId}/tasks` | List project tasks (filters: `status`, `priority`, `assigneeId`, `search`, `prState`, `page`, `pageSize`) |
| POST | `/projects/{projectId}/tasks` | Create task (auto-assigns sequential `taskNumber` & `taskKey`, e.g. `PRJ-ALPHA-1`) |
| GET | `/tasks/{taskId}` | Task details (includes `taskKey`, linked `branches`, and `pullRequests`) |
| PUT | `/tasks/{taskId}` | Update task fields |
| PATCH | `/tasks/{taskId}/status` | Change status |
| POST | `/tasks/{taskId}/comments` | Add comment |
| GET | `/tasks/{taskId}/comments` | List comments |
| GET | `/tasks/{taskId}/history` | Status history |

Status patch:
```json
{
  "status": "InProgress",
  "expectedVersion": 3
}
```
Return 409 when the version is stale or transition conflicts with current state.

## 5. Service desk

| Method | Route | Purpose |
|---|---|---|
| GET | `/tickets` | List tickets visible to caller |
| POST | `/tickets` | Create ticket |
| GET | `/tickets/{ticketId}` | Ticket details |
| PATCH | `/tickets/{ticketId}/assignment` | Assign ticket |
| PATCH | `/tickets/{ticketId}/status` | Change status |
| POST | `/tickets/{ticketId}/messages` | Add requester reply/internal note |
| GET | `/tickets/{ticketId}/messages` | List messages allowed for caller |

Requesters must never receive internal notes. Consider separate DTO/query paths for requester and support-agent views.

## 6. Approvals

| Method | Route | Purpose |
|---|---|---|
| GET | `/workflow-definitions` | List available definitions |
| POST | `/workflow-definitions` | Create definition |
| PUT | `/workflow-definitions/{id}` | Update definition |
| POST | `/approvals` | Submit request |
| GET | `/approvals` | List requests visible to caller |
| GET | `/approvals/{id}` | Request details and history |
| POST | `/approvals/{id}/decisions` | Approve/reject current step |
| POST | `/approvals/{id}/cancel` | Cancel own pending request if policy permits |

Decision request:
```json
{
  "decision": "Approved",
  "comment": "Reviewed and accepted.",
  "expectedVersion": 2
}
```

Approval decision must be performed transactionally: validate current state and approver, insert unique decision, update step/request state, write audit/activity and create notifications.

## 7. Notifications

| Method | Route | Purpose |
|---|---|---|
| GET | `/notifications` | Current user's notifications |
| PATCH | `/notifications/{id}/read` | Mark own notification read |
| POST | `/notifications/read-all` | Mark all own notifications read |

## 8. Dashboard and reports

| Method | Route | Purpose |
|---|---|---|
| GET | `/dashboard/summary` | Caller-specific summary |
| GET | `/reports/tasks` | Authorized task report |
| GET | `/reports/tickets` | Authorized ticket report |
| GET | `/reports/projects/{projectId}` | Project summary |

Use bounded date ranges and pagination. CSV export can be a later feature.

## 9. Administration and audit

| Method | Route | Purpose |
|---|---|---|
| GET | `/admin/users` | List users |
| POST | `/admin/users` | Create/invite user |
| PATCH | `/admin/users/{id}/status` | Activate/deactivate |
| PUT | `/admin/users/{id}/roles` | Replace approved roles |
| GET | `/admin/audit-logs` | Filter audit events |

## 10. Git Integration & Webhooks

| Method | Route | Purpose |
|---|---|---|
| GET | `/projects/{projectId}/git/repositories` | List connected Git repositories for a project |
| POST | `/projects/{projectId}/git/repositories` | Connect GitHub repository (Owner/Manager/Admin; returns one-time generated webhook secret) |
| PUT | `/projects/{projectId}/git/repositories/{repositoryId}` | Update repository credentials or automation toggles |
| DELETE | `/projects/{projectId}/git/repositories/{repositoryId}` | Disconnect repository |
| POST | `/projects/{projectId}/git/repositories/{repositoryId}/test` | Test GitHub API PAT & repo access |
| POST | `/projects/{projectId}/git/repositories/{repositoryId}/sync` | Manually sync PRs, CI checks, reviews & task links |
| GET | `/projects/{projectId}/git/pull-requests` | List synced PRs for a project (optional `state` filter) |
| GET | `/tasks/{taskId}/git` | Get linked branches & PRs for a task |
| POST | `/tasks/{taskId}/git/branches` | Create a Git branch directly from a Kanban card or task |
| POST | `/tasks/{taskId}/git/pull-requests/link` | Manually link a PR (`repositoryId`, `prNumber`) to a task |
| DELETE | `/tasks/{taskId}/git/pull-requests/{pullRequestId}` | Unlink a PR from a task |
| POST | `/webhooks/git/{provider}/{repositoryId}` | `[AllowAnonymous]` inbound webhook verified via `X-Hub-Signature-256` (HMAC-SHA256) & `X-GitHub-Delivery` idempotency (`WebhookPolicy` rate limited) |

## 11. HTTP status guidance

- 200: successful read/update with response
- 201: created resource
- 204: successful action without body
- 400: validation failure
- 401: unauthenticated
- 403: authenticated but forbidden
- 404: not found or resource intentionally concealed
- 409: concurrency/state conflict
- 429: rate limit exceeded
- 500: unexpected server error (safe generic response)

## 12. API contract verification

During implementation:
1. Generate OpenAPI from the running API.
2. Compare actual routes, schemas and authorization against this document.
3. Add integration tests for representative success and failure paths.
4. Update this document when intentional contract changes are made.
