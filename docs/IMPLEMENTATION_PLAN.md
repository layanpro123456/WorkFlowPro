# WorkFlowPro – Implementation Plan

## Guiding rule

Build vertical slices. Each slice must include domain/application logic, persistence, API, UI, tests and documentation before moving on. Do not scaffold every feature and leave incomplete placeholders.

## Phase 0 – Repository and development environment

Tasks:
- Create Git repository and solution.
- Create backend projects and Angular workspace.
- Add formatting/editor configuration and `.gitignore`.
- Add Docker Compose PostgreSQL service and `.env.example`.
- Add health endpoint and initial OpenAPI.
- Add CI skeleton for build/test.

Exit criteria:
- Clean clone can restore and build.
- PostgreSQL starts locally.
- API health endpoint responds.
- No secrets committed.

## Phase 1 – Foundation and identity

Tasks:
- Configure EF Core/Npgsql and Identity.
- Create user extension fields and roles.
- Implement login, refresh rotation, logout and `/auth/me`.
- Add global Problem Details handling and validation.
- Add Angular app shell, login page, auth service and HTTP interceptor.
- Implement role-aware navigation (not security enforcement).

Exit criteria:
- Login works against PostgreSQL.
- Refresh rotation and logout are tested.
- Protected endpoint rejects anonymous request.
- Role assignment cannot be self-granted.
- Angular handles expired/invalid sessions safely.

## Phase 2 – Projects and membership

Tasks:
- Implement project entity, membership and project policies.
- Implement create/list/detail/update/archive.
- Implement add/remove member.
- Build Angular project list/detail/member screens.
- Add search, pagination and form validation.

Exit criteria:
- Project owner/manager can manage project.
- Non-member cannot read restricted project.
- Unique code and date rules are enforced.
- Integration tests pass.

## Phase 3 – Tasks

Tasks:
- Implement task lifecycle and status transition policy.
- Implement comments and status history.
- Add optimistic concurrency.
- Build task list/detail/form and Kanban view.
- Add filters for status, priority, assignee and due date.

Exit criteria:
- Assignee must be active project member.
- Invalid transitions rejected.
- Stale updates return 409.
- History records every status change.
- Core task workflow works end-to-end.

## Phase 4 – Service desk

Tasks:
- Implement ticket creation, assignment, status and messages.
- Separate public replies from internal notes.
- Add ticket categories and priority.
- Build requester and support-agent screens.

Exit criteria:
- Requester cannot see internal notes.
- Assignment restricted to authorized roles.
- Ticket lifecycle is audited and tested.

## Phase 5 – Approval workflows

Tasks:
- Implement workflow definition and ordered steps.
- Snapshot steps/approvers when request is submitted.
- Implement transactional approval/rejection.
- Add comments and history.
- Build workflow configuration and approval inbox/detail screens.

Exit criteria:
- Only current approver can decide.
- A step cannot be decided twice.
- Rejection ends request.
- Final approval completes request.
- Concurrency tests pass.

## Phase 6 – Notifications, dashboard and reports

Tasks:
- Create in-app notifications from relevant events.
- Add read/unread behavior.
- Implement authorized dashboard projections.
- Implement date-filtered task/ticket reports.
- Add CSV export only after report correctness is tested.

Exit criteria:
- Users cannot access other users' notifications.
- Dashboard is based on real persisted data.
- Aggregations respect resource access rules.

## Phase 7 – Admin, audit and hardening

Tasks:
- User administration and role management.
- Audit log filtering.
- Rate limiting, CORS, security headers and production error behavior.
- Logging and health checks.
- Dependency review and security test pass.

Exit criteria:
- Privileged actions are audited.
- No critical authorization/privacy defect is known.
- Production configuration contains no hard-coded secrets.

## Phase 8 – Deployment and portfolio presentation

Tasks:
- Dockerfiles and Compose for local/demo setup.
- CI build/test workflow.
- Production deployment configuration.
- Database migration and backup/restore instructions.
- README screenshots, ERD, architecture diagram and demo video.
- Seed synthetic demo accounts through an explicit development/demo-only mechanism.

Exit criteria:
- Reproducible deployment from documented steps.
- HTTPS enabled for public deployment.
- Demo uses synthetic data.
- Tests and CI pass.
- All claims in CV/README verified against actual implementation.

## Suggested schedule

A realistic solo schedule is 4–8 weeks depending on daily availability and experience. Do not sacrifice authorization, tests or documentation to meet an arbitrary deadline.
