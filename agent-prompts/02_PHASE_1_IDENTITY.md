# Prompt – Phase 1: Identity Foundation

Read master instructions and relevant requirements, architecture, database, API and security docs.

Implement identity only:
- ASP.NET Core Identity persisted in PostgreSQL.
- User extension fields: display name, active flag, created timestamp.
- Roles: Administrator, Manager, Employee, SupportAgent.
- Administrator-controlled user creation/role assignment; no public registration.
- Login, short-lived access token, refresh-token rotation, logout and `/api/v1/auth/me`.
- Store refresh-token hashes server-side; use Secure/HttpOnly cookie for browser refresh token.
- Revoke on logout and account deactivation; implement reuse detection or document tested limitation.
- Authentication rate limiting, CORS configuration, Problem Details and validation.
- Angular login screen, auth service, interceptor, route guard for navigation and role-aware shell.

Do not implement projects/tasks/tickets/approvals.

Security constraints:
- Never store refresh token in localStorage.
- Never let a user grant their own roles.
- Do not return secrets or token hashes.
- Test inactive users and unauthorized endpoints.

Use PostgreSQL-backed integration tests, not only EF InMemory.

Acceptance: see Phase 1 in `docs/IMPLEMENTATION_PLAN.md`. Run and report actual backend/frontend tests and builds.
