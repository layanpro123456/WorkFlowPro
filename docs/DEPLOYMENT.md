# Jiva – Deployment and Operations

## 1. Environments

- Development: local API, Angular dev server, PostgreSQL via Docker.
- Test: isolated PostgreSQL database/container, automated tests.
- Demo/production: containerized API and database service or managed PostgreSQL; Angular static hosting or web container.

Never share production credentials with local development.

## 2. Configuration

Use environment-specific configuration. `.env.example` documents names only, never real values.

Expected configuration categories:
- ASP.NET Core environment
- PostgreSQL connection string
- JWT signing key / issuer / audience
- Access-token lifetime
- Refresh-token cookie settings
- Allowed frontend origins
- Logging level
- File storage path (only if file feature implemented)

Use strong random secrets and a secret manager for public deployment. Do not use development signing keys in production.

## 3. Docker plan

Compose services:
- `db`: PostgreSQL with named volume and health check
- `api`: ASP.NET Core API, waits for healthy database
- `web`: Angular production build served by a web server (optional for local stack)

Do not expose PostgreSQL publicly in production. Use a private network and least-privilege credentials.

## 4. Database migrations

- Generate EF Core migrations from Infrastructure project.
- Review migration SQL before production.
- Apply migrations as a controlled deployment step, not concurrently from every API replica.
- Back up before schema changes.
- Test migration from an empty database and from the previous release schema.
- Never manually edit production schema without reconciling EF migrations.

## 5. Health and observability

- Liveness endpoint indicates process is running.
- Readiness endpoint checks dependencies required to serve requests.
- Structured logs include correlation ID.
- Do not include secrets or private ticket content in logs.
- Monitor API errors, latency, database connectivity and disk space.

## 6. CI/CD

GitHub Actions should:
- Build backend in Release.
- Run unit and integration tests.
- Build/test Angular.
- Build container images.
- Publish artifacts/images only from approved branches/tags.
- Use repository/environment secrets for deployment credentials.

## 7. Backup and recovery

- Schedule PostgreSQL backups for hosted deployment.
- Encrypt backup storage.
- Restrict restore permissions.
- Test restore periodically.
- Document recovery steps and acceptable data-loss/recovery-time objectives for the demo environment.

## 8. Demo deployment checklist

- [ ] HTTPS and secure headers configured
- [ ] Production secrets stored outside repository
- [ ] PostgreSQL not publicly exposed
- [ ] Migrations applied and verified
- [ ] Demo accounts have synthetic data only
- [ ] Admin password changed from initial seed value
- [ ] Logs contain no credentials/tokens
- [ ] Backup/restore procedure documented
- [ ] Health/readiness checks work
- [ ] CI build and tests pass
