# WorkFlowPro – Architecture

## 1. Architecture style

Use a pragmatic Clean Architecture / layered architecture with dependency inversion.

```text
Angular SPA
    |
    | HTTPS / JSON / Bearer token
    v
ASP.NET Core API
    |
    v
Application layer  ----> Domain layer
    |
    v
Infrastructure layer
    |
    v
PostgreSQL
```

Dependency rule:
- Domain has no dependency on other project layers.
- Application depends on Domain.
- Infrastructure implements interfaces defined by Application (and uses Domain).
- API composes dependencies and calls Application.
- Frontend communicates only through documented HTTP contracts.

## 2. Backend projects

### WorkFlowPro.Domain
Contains:
- Entities and aggregate roots
- Value objects/enums
- Domain invariants
- Domain exceptions/events where useful

Must not contain:
- EF Core DbContext
- ASP.NET Core types
- HTTP concerns
- Database-specific code

### WorkFlowPro.Application
Contains:
- Use cases/application services
- Request/response DTOs
- Validation
- Authorization abstractions/resource access checks
- Repository or query abstractions only where they add value
- Interfaces for clock, current user, file storage and notification services

Prefer explicit use-case services over introducing a generic repository/unit-of-work abstraction that merely duplicates EF Core.

### WorkFlowPro.Infrastructure
Contains:
- EF Core DbContext and entity configurations
- PostgreSQL/Npgsql configuration
- Identity persistence
- Implementations of application interfaces
- Migrations
- Data seeding for development only
- File storage adapter for local development if implemented

### WorkFlowPro.Api
Contains:
- Controllers or minimal API endpoints (choose one style consistently; recommended: controllers for clear portfolio demonstration)
- Authentication/authorization setup
- Middleware and exception mapping
- OpenAPI configuration
- Dependency injection composition
- Health checks
- API versioning strategy if introduced

## 3. Frontend structure

```text
src/app/
├── core/
│   ├── auth/
│   ├── guards/
│   ├── interceptors/
│   ├── layout/
│   └── services/
├── shared/
│   ├── components/
│   ├── models/
│   ├── pipes/
│   └── validators/
└── features/
    ├── dashboard/
    ├── projects/
    ├── tasks/
    ├── tickets/
    ├── approvals/
    ├── reports/
    └── administration/
```

Use standalone Angular components and feature-level routing. Keep API access in services. Do not put authorization decisions only in route guards; the API remains authoritative.

## 4. Request lifecycle

1. Angular sends HTTP request with access token.
2. ASP.NET Core authentication validates token.
3. Authorization policy checks role/claim/resource access.
4. API validates transport DTO.
5. Application use case executes business logic.
6. Domain invariants are enforced.
7. Infrastructure persists changes in PostgreSQL.
8. API maps result to response DTO.
9. Angular presents success/error/loading state.

## 5. Cross-cutting concerns

- Global exception middleware maps known exceptions to Problem Details.
- Validation returns consistent 400 responses.
- Structured logging includes request/correlation ID.
- Health endpoints expose liveness and readiness appropriately.
- Use UTC clock abstraction in business logic and tests.
- Use pagination consistently.
- Use optimistic concurrency for tasks and approval steps.
- Use database transactions for multi-entity state changes.

## 6. API conventions

- Base path: `/api/v1`
- JSON camelCase
- ISO 8601 UTC timestamps
- IDs: UUID
- Pagination: `page` (1-based), `pageSize` (bounded, default 20, max 100)
- Standard error format: RFC 7807 Problem Details
- Return 201 + Location for created resources where appropriate
- Return 204 for successful no-content operations
- Return 404 when resource is absent or intentionally concealed due to access policy
- Return 409 for concurrency conflicts or invalid state conflicts

## 7. Authentication decision

Use ASP.NET Core Identity for password hashing and user lifecycle. Use short-lived JWT access tokens for API calls. For a portfolio-grade implementation, explicitly document token revocation limitations and choose one of:
- Short-lived access tokens plus rotating refresh tokens stored hashed server-side; revoke refresh token on logout.
- A server-side cookie/BFF model if browser security and token storage complexity are prioritized.

For this SPA/API project, preferred design is short-lived access tokens plus refresh-token rotation, with refresh tokens stored securely (HttpOnly Secure SameSite cookie for browser clients) and hashed in the database. Do not store long-lived bearer tokens in localStorage.

## 8. Data consistency

- PostgreSQL is source of truth.
- Use FK constraints and unique indexes.
- Use transactions for workflow decisions and multi-record changes.
- Use concurrency tokens or explicit version columns; PostgreSQL xmin can be used carefully, but an explicit application-managed version column is easier to explain portably.
- Use soft archive for projects; do not soft-delete every entity by default.
- Audit events should be append-only through application code and restricted database permissions where practical.

## 9. Architecture decisions to record

Create ADRs when implementation begins:
- ADR-001: Controllers vs minimal APIs
- ADR-002: Authentication and refresh-token strategy
- ADR-003: EF Core query/repository approach
- ADR-004: File storage approach
- ADR-005: Sequential workflow model
- ADR-006: Deployment topology
