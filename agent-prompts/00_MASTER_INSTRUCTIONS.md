# Antigravity Master Instructions – WorkFlowPro

You are implementing WorkFlowPro, a portfolio project. Read `README.md` and all relevant files under `docs/` before changing code.

## Fixed technology decisions
- ASP.NET Core Web API on .NET 10
- Angular + TypeScript
- PostgreSQL
- EF Core with Npgsql provider, with compatible package versions
- ASP.NET Core Identity
- xUnit and API integration tests
- Docker and GitHub Actions

## Critical rules
1. The repository may contain no implementation yet. Inspect before assuming.
2. Never claim code, tests, deployment or features exist unless verified in the repository and by commands.
3. Do not invent requirements, business rules, package APIs or credentials.
4. Ask questions when a decision materially affects security, data integrity or scope. Otherwise state a conservative assumption.
5. Implement only the requested phase. Do not scaffold all modules as empty placeholders.
6. Keep Domain independent from EF Core and ASP.NET Core.
7. Use DTOs; never expose EF entities directly.
8. Enforce authorization in the API, including resource-level checks.
9. Use PostgreSQL-compatible EF Core migrations and test against PostgreSQL.
10. Add tests for success, validation failure, unauthorized access and concurrency where relevant.
11. Do not use EF Core InMemory as the only persistence integration test.
12. Use async I/O and CancellationToken.
13. Do not commit secrets, real personal data or production connection strings.
14. Before destructive commands or migrations against non-test databases, stop and request confirmation.
15. Keep docs synchronized with actual implementation status.

## Required response after each task
- Summary of changes
- Files created/modified
- Design decisions and assumptions
- Commands run
- Actual build/test results (include failures)
- Remaining work and risks
- Suggested next small task

Start by inspecting the repository and reporting its current state. Do not implement until asked for a specific phase.
