# Jiva – Software Requirements Specification

**Version:** 1.0  
**Status:** Proposed requirements; implementation not yet verified.

## 1. Purpose

WorkFlowPro is a single-organization enterprise workflow application for managing projects, tasks, internal support requests and approval processes. It is designed as a demonstrable full-stack engineering portfolio project.

## 2. Goals

- Demonstrate end-to-end development using ASP.NET Core 10 and Angular.
- Provide secure role-based access to business functions.
- Demonstrate relational data modeling with PostgreSQL and EF Core.
- Demonstrate maintainable architecture, validation, testing and deployment.
- Provide a realistic workflow that can be explained in a technical interview.

## 3. Out of scope for the first release

- Multi-tenant SaaS billing and tenant isolation
- Native mobile applications
- External email/SMS delivery
- AI-generated decisions or autonomous ticket resolution
- Complex BPMN designer
- Payroll, HR and accounting
- Real enterprise SSO integrations
- File storage on public cloud (local development may use a local storage adapter)

## 4. User roles

Roles are initial application roles. The authorization model must be enforced by the API.

| Role | Intended access |
|---|---|
| Administrator | User/role administration, configuration, audit access |
| Manager | Manage assigned projects, memberships, approvals and team reporting |
| Employee | Work on assigned projects/tasks, create and manage own tickets |
| Support Agent | View and process assigned support tickets |

A user may hold more than one role. Project membership and ticket assignment provide additional resource-level authorization.

## 5. Functional requirements

### FR-01 Identity and access

- Register users through an administrator-controlled workflow; public self-registration is disabled by default.
- Authenticate users and issue short-lived access tokens.
- Support logout/revocation strategy appropriate to the selected token implementation.
- Retrieve the current user's profile and roles.
- Administrators can activate/deactivate users and assign approved roles.
- Passwords must be securely hashed by ASP.NET Core Identity.
- Protect API endpoints using authentication and authorization policies.

Acceptance:
- Anonymous users cannot access protected endpoints.
- A deactivated user cannot continue to authenticate.
- A user cannot grant themselves roles.
- Authorization is tested at endpoint and resource levels.

### FR-02 Projects

- Authorized users can create, view, update and archive projects.
- A project has a unique project code, name, description, status, start date and optional target date.
- Managers can add/remove project members and assign project roles.
- Project members can view project details subject to access rules.
- Archived projects are read-only except for authorized administrators.

Acceptance:
- Project code uniqueness is enforced by the database.
- Non-members cannot access private project details.
- Invalid date ranges are rejected.
- Archive does not physically delete historical tasks.

### FR-03 Tasks

- Authorized project members can create tasks within a project.
- Tasks have title, description, status, priority, assignee, due date and optional estimate.
- Task statuses: Backlog, ToDo, InProgress, Blocked, Done, Cancelled.
- Priority values: Low, Normal, High, Urgent.
- Users can update tasks subject to project role and assignment rules.
- Task comments are recorded with author and timestamp.
- Task status changes create activity history.

Acceptance:
- Assignee must be an active project member.
- A task cannot reference a project the caller cannot access.
- Invalid status transitions are rejected according to documented transition rules.
- Concurrent updates must not silently overwrite newer data; use optimistic concurrency.

### FR-04 Service desk tickets

- Employees can create tickets with subject, description, category, priority and optional related project.
- Support agents can assign tickets to themselves or another authorized agent.
- Ticket statuses: New, Assigned, InProgress, WaitingForRequester, Resolved, Closed.
- Requesters can view their tickets and add replies.
- Support agents can add internal notes that requesters cannot see.
- Resolution and closure actions record actor and timestamp.

Acceptance:
- Internal notes are never returned to requester-facing API responses.
- Only support agents/managers/admins can assign tickets.
- Requesters cannot change internal assignment or priority.
- All status changes are audited.

### FR-05 Approval workflows

First release uses a simple sequential approval model.

- A manager or authorized user can create an approval request.
- Request has title, description, requester, current step and status.
- A workflow definition contains ordered approval steps and assigned role or specific approver.
- Approver can approve or reject the current step with a comment.
- Rejection ends the request as Rejected.
- Completion of all steps marks request Approved.
- Requester and authorized approvers can view status and history.

Acceptance:
- Only the current assigned approver can decide a step.
- A step cannot be decided twice.
- Decisions and comments are immutable history records.
- Concurrent decisions cannot approve the same step twice.

### FR-06 Notifications and activity

- In-app notifications are created for assignment, comments, approval decisions and relevant status changes.
- Users can list notifications and mark them read.
- Important project/task/ticket/approval changes create activity records.
- Notification delivery is in-app only in first release.

Acceptance:
- Users can only read/update their own notifications.
- Activity records include actor, action, entity type, entity ID and timestamp.
- Do not store secrets or sensitive data in activity payloads.

### FR-07 Dashboard and reporting

- Dashboard summary is based on the signed-in user's authorized data.
- Show counts for assigned tasks, overdue tasks, open tickets and pending approvals.
- Managers can view project-level summaries for projects they manage.
- Provide date-filtered task and ticket reports.
- CSV export may be included after core reports are complete.

Acceptance:
- Dashboard numbers are derived from persisted data.
- API queries enforce authorization before aggregation.
- Date filters use explicit inclusive/exclusive semantics and UTC handling.
- Reports are paginated or bounded to avoid unbounded queries.

### FR-08 Administration and audit

- Admin can manage users and roles.
- Admin can view audit records with filters.
- Audit records are append-only through application services.
- Audit events include actor, action, entity, timestamp and safe metadata.

Acceptance:
- Non-admin users cannot access admin endpoints.
- Audit records cannot be edited/deleted through normal application APIs.
- Audit metadata does not contain passwords, tokens or secrets.

## 6. Non-functional requirements

### Security
- OWASP-aligned input validation, access control and secure configuration.
- HTTPS outside local development.
- Rate limiting on authentication endpoints.
- CORS restricted to configured frontend origins.
- Secrets supplied through environment variables or secret stores.
- Safe error responses without stack traces in production.

### Performance
- Paginate list endpoints.
- Add indexes for common filters and foreign keys.
- Avoid N+1 database access.
- Use projections for list/read endpoints.
- Establish performance baselines rather than claiming arbitrary throughput.

### Reliability
- Use database transactions for multi-record business operations.
- Use optimistic concurrency for mutable workflows.
- Health checks for API and database connectivity.
- Structured logs with correlation/request identifiers.

### Accessibility and usability
- Responsive desktop/tablet/mobile layouts.
- Keyboard-accessible controls and visible focus states.
- Clear loading, empty, validation and error states.
- Confirm destructive or irreversible actions.

### Maintainability
- Separate Domain, Application, Infrastructure and API.
- DTOs and explicit mapping.
- Automated unit/integration tests.
- Consistent naming and formatting.
- Document architectural decisions.

## 7. Main user journeys

1. Administrator creates users and assigns roles.
2. Manager creates a project and adds members.
3. Project member creates a task and assigns it to a project member.
4. Assignee updates task status and adds comments.
5. Employee raises a support ticket.
6. Support agent assigns and resolves the ticket.
7. Employee submits an approval request.
8. Assigned approvers approve/reject in sequence.
9. Manager views authorized dashboard/report summaries.

## 8. Release acceptance

The MVP is acceptable only when:
- Identity and role authorization work end-to-end.
- Project/task workflow works end-to-end.
- Ticket workflow works end-to-end.
- Sequential approval workflow works end-to-end.
- PostgreSQL migrations apply to an empty database.
- Backend unit and integration tests pass.
- Frontend tests and production build pass.
- Docker-based local setup is documented and verified.
- No known critical security issue remains.
- README clearly distinguishes implemented functionality from planned functionality.
