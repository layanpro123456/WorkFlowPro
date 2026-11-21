# WorkFlowPro

**Enterprise Project, Task, Service Desk and Approval Workflow Platform**

WorkFlowPro is a portfolio project intended to demonstrate practical full-stack enterprise application engineering with ASP.NET Core 10, Angular, PostgreSQL, automated testing and containerized deployment.

> **Project status:** Phase 7 (Administration, Audit Records, RBAC Governance & System Hardening) completed and verified.
> 
> - **Backend:** .NET 9 (`net9.0`) Clean Architecture with ASP.NET Core Identity & EF Core Npgsql migrations.
> - **Projects & Membership:** Project lifecycle and member management (Owner, Manager, Member) with resource concealment for unauthorized access.
> - **Tasks & Workflow:** Complete task lifecycle (`Backlog`, `ToDo`, `InProgress`, `Blocked`, `Done`, `Cancelled`) with documented transition matrix (`docs/TASK_TRANSITIONS.md`), optimistic concurrency (`version` token mapped to HTTP 409 Conflict), active-member assignee validation, threaded comments, and immutable status audit history.
> - **Service Desk & Internal Notes Privacy:** End-to-end support ticket system with ticket lifecycle (`New`, `Assigned`, `InProgress`, `WaitingForRequester`, `Resolved`, `Closed`), sequential human-friendly ticket numbers (`#1001+`), strict internal notes privacy (internal notes filtered out at data layer for requesters; requesters strictly forbidden from creating internal notes mapped to HTTP 403 Forbidden), agent-only assignment and assignee listings, and audit history.
> - **Approval Workflows Engine:** Multi-step sequential approval hierarchies with snapshotted steps and resolved concrete approvers at submission time (`docs/APPROVAL_WORKFLOWS.md`), documented deterministic role resolution policy with optional requester override, database-level unique constraint preventing double decision (`unique(approval_request_step_id)`), strict designated-approver-only authorization (HTTP 403 Forbidden for unauthorized attempts), optimistic concurrency checking (`version` token mapped to HTTP 409 Conflict), terminal rejection (skips remaining steps), and full approval completion.
> - **In-App Notifications & Activity Events:** Automated in-app notifications generated for task assignments and discussion comments, ticket status updates and agent/customer replies, and multi-step approval decisions. Strict user isolation ensures users can only query and mark their own notifications as read (attempts to mark others' notifications return HTTP 404 to avoid ID enumeration).
> - **Authorized Dashboard & Bounded Analytics:** High-performance dashboard summary with caller-authorized projections for assigned tasks, overdue tasks, open tickets, pending approvals, unread notifications, and managed projects. Bounded date-filtered reports for Tasks and Tickets with status/category/priority distribution breakdowns, pagination, and RFC 4180 compliant CSV exports with proper comma/quote escaping.
> - **Administration, Audit Logs & System Hardening:** Administrative user provisioning and role management (`Administrator`, `Manager`, `Employee`, `SupportAgent`), user activation/deactivation with automatic active refresh token revocation, sole-admin protection rules, immutable `audit_logs` tracking of privileged actions, IP-partitioned rate limiting with HTTP 429 enforcement, comprehensive security headers (`nosniff`, `DENY`, `CSP`, `Referrer-Policy`), safe production exception handling with `correlationId` tracking, and zero vulnerable dependencies across .NET and npm.
> - **Frontend:** Angular 22 standalone with responsive Kanban Board & Table views, transition dropdowns, quick moves, task creation modal with member picker, task detail view with live discussion and audit history timeline, Service Desk list view with tab switcher (All Tickets vs My Tickets), ticket submission modal, rich ticket detail view with internal note warnings and agent tools, Approvals suite featuring My Inbox, My Submitted Requests, workflow hierarchy previewer, sequential visual stepper, approver decision actions, notification bell with unread badge and quick navigation dropdown, caller KPI Dashboard, Reports & Analytics console with live CSV export downloads, and dedicated Administration Suite featuring user provisioning modals, RBAC role editors, user status toggles, and filterable Audit Log Explorer.
> - **Verification:** 100% test pass on backend unit tests (165/165), integration tests (53/53) for a total of 218 backend tests, and frontend Vitest suite (82/82 across 22 test files). Production build passing with zero warnings. Zero vulnerabilities reported by `dotnet list package --vulnerable` and `npm audit`.

## Technology decisions

- Backend: ASP.NET Core Web API on .NET 10
- Frontend: Angular + TypeScript
- Database: PostgreSQL
- Data access: Entity Framework Core with the Npgsql provider
- Authentication: ASP.NET Core Identity with bearer-token authentication
- API documentation: OpenAPI
- Tests: xUnit, ASP.NET Core integration tests, frontend unit tests
- Local environment: Docker Compose
- CI: GitHub Actions

Use compatible stable package versions and Angular releases at implementation time. Do not mix EF Core major versions.

## Product modules

1. Identity, users, roles and permissions
2. Projects, memberships and tasks
3. Service desk tickets and assignment
4. Configurable approval workflows
5. Notifications and activity history
6. Dashboard and reporting
7. Administration and audit records

## Repository layout

```text
WorkFlowPro/
├── backend/
│   ├── WorkFlowPro.sln
│   └── src/
│       ├── WorkFlowPro.Api/
│       ├── WorkFlowPro.Application/
│       ├── WorkFlowPro.Domain/
│       └── WorkFlowPro.Infrastructure/
├── frontend/
│   └── workflowpro-web/
├── tests/
│   ├── WorkFlowPro.UnitTests/
│   └── WorkFlowPro.IntegrationTests/
├── docs/
├── agent-prompts/
├── deploy/
├── .github/workflows/
├── docker-compose.yml
└── README.md
```

## Local development prerequisites

- Git
- .NET 10 SDK
- Node.js version supported by the selected Angular release
- Angular CLI matching the selected Angular release
- Docker Desktop or Docker Engine with Compose
- PostgreSQL client (optional; Docker supplies the database)

## Planned local startup

The exact commands and environment variables must be implemented and verified in the deployment phase. Intended flow:

1. Copy `.env.example` to `.env`.
2. Set local-only secrets and database credentials.
3. Start PostgreSQL with Docker Compose.
4. Apply EF Core migrations.
5. Start the ASP.NET Core API.
6. Start the Angular application.
7. Open the API OpenAPI page and frontend URL.

Never commit `.env`, production secrets, signing keys or real user data.

## Engineering rules

- Keep business rules in the Domain/Application layers, not controllers.
- Controllers should be thin HTTP adapters.
- Use async APIs and pass `CancellationToken` through I/O operations.
- Validate input at the API boundary and enforce business invariants in the domain/application layer.
- Use DTOs; never expose EF entities directly.
- Use UTC timestamps in storage and ISO 8601 in APIs.
- Use decimal/numeric types for monetary values, never floating point.
- Enforce authorization on the server, not only in Angular.
- Use database constraints and transactions where correctness requires them.
- Do not log passwords, tokens, secrets or sensitive ticket content.
- Do not claim a feature is complete until implementation and tests pass.

## Documentation index

- [Requirements](docs/REQUIREMENTS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Database design](docs/DATABASE.md)
- [API specification](docs/API_SPECIFICATION.md)
- [Security](docs/SECURITY.md)
- [Testing strategy](docs/TESTING.md)
- [Implementation plan](docs/IMPLEMENTATION_PLAN.md)
- [Deployment](docs/DEPLOYMENT.md)
- [Definition of done](docs/DEFINITION_OF_DONE.md)
- [Antigravity workflow](docs/ANTIGRAVITY_WORKFLOW.md)
- [Agent prompts](agent-prompts/)

## Portfolio presentation checklist

Only check these items after completing and verifying them:

- [ ] Public GitHub repository with clean commit history
- [ ] Working local setup from documented instructions
- [ ] Live demo deployment
- [ ] Screenshots and short demo video
- [ ] OpenAPI documentation
- [ ] Database ER diagram
- [ ] Unit and integration test results
- [ ] CI workflow passing
- [ ] Security and known-limitations sections
- [ ] Clear explanation of architectural decisions
