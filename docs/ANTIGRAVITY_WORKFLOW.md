# Antigravity Implementation Workflow

## Objective

Use Antigravity as an implementation agent while keeping the developer responsible for requirements, review, testing and final claims.

## Before each phase

1. Open the repository root in Antigravity.
2. Read `README.md`, relevant requirements and architecture docs.
3. Ask the agent to inspect the existing repository before editing.
4. Ask for a short implementation plan and list of files to change.
5. Confirm scope; avoid unrelated refactoring.
6. Implement one vertical slice at a time.

## During implementation

- Require the agent to explain assumptions.
- Require it to preserve existing working behavior.
- Require migrations for schema changes.
- Require tests for business rules and authorization.
- Require commands and actual test output.
- Ask it to stop and report blockers rather than invent APIs or package versions.
- Review diffs before accepting changes.
- Commit each working phase separately.

## After each phase

Run the relevant commands yourself or inspect their output:
- `dotnet build`
- `dotnet test`
- frontend test command from package scripts
- frontend production build
- Docker Compose startup and health checks where applicable

Do not accept claims such as "all tests pass" without seeing test output.

## Agent safety

- Do not give unrestricted access to production systems or credentials.
- Do not paste real customer data into the agent.
- Review shell commands, migrations, destructive operations and dependency additions.
- Keep `.env` out of Git.
- Prefer least-privilege permissions.
- Make backups before database changes.

## Recommended cadence

One issue/feature branch per vertical slice:
- `feature/identity-foundation`
- `feature/projects`
- `feature/tasks`
- `feature/service-desk`
- `feature/approval-workflows`
- `feature/reporting`
- `feature/deployment`

Use conventional commits, for example:
- `feat(tasks): add task status transitions`
- `test(approvals): cover duplicate decision conflict`
- `docs(api): document ticket endpoints`
