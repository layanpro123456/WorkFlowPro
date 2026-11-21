# Prompt – Phase 0: Repository Foundation

Read `agent-prompts/00_MASTER_INSTRUCTIONS.md`, `docs/ARCHITECTURE.md`, `docs/DEPLOYMENT.md` and `docs/DEFINITION_OF_DONE.md`.

Implement Phase 0 only:
- Create .NET 10 solution and four projects: API, Application, Domain, Infrastructure.
- Set correct project references following Clean Architecture.
- Create Angular workspace using a stable Angular release compatible with the installed Node.js.
- Add PostgreSQL Docker Compose service with health check and named volume.
- Add `.env.example`, `.gitignore`, editor configuration and initial README commands.
- Add a minimal API health endpoint and OpenAPI setup.
- Add a GitHub Actions build workflow skeleton.

Do not implement business modules or authentication yet.

Before editing, inspect installed SDK/Node versions and existing repository files. Report incompatible/missing prerequisites rather than silently changing machine configuration.

Acceptance:
- `dotnet build` succeeds.
- Angular production build succeeds.
- PostgreSQL starts and reports healthy.
- API health endpoint responds.
- No secrets are committed.
- Project references obey architecture rules.

Run commands and report actual output. Do not claim success without verification.
