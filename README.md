# WorkFlowPro

**Enterprise Project, Task, Service Desk and Approval Workflow Platform**

WorkFlowPro is a portfolio project intended to demonstrate practical full-stack enterprise application engineering with ASP.NET Core 10, Angular, PostgreSQL, automated testing and containerized deployment.

> **Project status:** Phase 1 (Foundation and Identity) completed and verified.
> 
> - **Backend:** .NET 9 (`net9.0`) with ASP.NET Core Identity & EF Core Npgsql PostgreSQL migrations.
> - **Authentication:** JWT access tokens (in-memory) + rotating refresh tokens with SHA-256 server-side hashing stored via HttpOnly Secure SameSite cookies.
> - **Frontend:** Angular 22 standalone workspace with AuthService, HttpInterceptor, AuthGuard, role-aware shell, accessible login form, and dashboard.
> - **Verification:** 100% test pass on all backend unit tests (12/12), integration tests (11/11), and frontend Vitest suite (9/9).

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
