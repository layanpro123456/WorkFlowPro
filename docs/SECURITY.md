# Jiva – Security Requirements

## Authentication
- Use ASP.NET Core Identity password hashing; never implement custom password hashing.
- Disable public registration by default.
- Use short-lived access tokens.
- Rotate refresh tokens on use and store only token hashes.
- Revoke refresh tokens on logout, password reset and account deactivation.
- Consider reuse detection and revocation of a token family.
- Rate-limit login and refresh endpoints.
- Avoid revealing whether an email address exists in login/reset responses.

## Authorization
- Apply `[Authorize]` or equivalent policies by default to protected routes.
- Define role policies for Administrator, Manager, Employee and SupportAgent.
- Add resource-level checks for project membership, ticket ownership and approval assignment.
- Never rely on Angular route guards as security controls.
- Prevent mass assignment by using narrow request DTOs.
- Validate role assignment on the server.

## Browser security
- Use HTTPS outside local development.
- Keep refresh tokens in Secure, HttpOnly cookies with appropriate SameSite settings.
- Do not put refresh tokens in localStorage/sessionStorage.
- If cookie-based refresh is used, implement CSRF protections appropriate to deployment.
- Restrict CORS to known frontend origins.
- Apply a suitable Content Security Policy in deployment.

## Data and secrets
- Store secrets outside source control.
- Use environment variables locally and secret stores in production.
- Never log passwords, access/refresh tokens, connection strings or sensitive content.
- Use least-privilege database credentials.
- Encrypt backups and restrict access.
- Avoid returning internal ticket notes to requesters.
- Audit role changes and privileged actions.

## API and input safety
- Validate length, range, format and allowed enum values.
- Use EF Core parameterization; never concatenate untrusted SQL.
- Bound pagination and report date ranges.
- Limit request body and upload size.
- If file uploads are added, validate type/content, generate server-side names, store outside web root and scan where practical.
- Return generic production errors with a correlation ID; do not expose stack traces.

## Operational security
- Keep .NET, Angular and npm dependencies patched.
- Review dependency vulnerability alerts.
- Add security-focused integration tests.
- Configure secure headers.
- Back up PostgreSQL and test restore procedures.
- Document known limitations and threat assumptions.

## Portfolio demo safety
- Use synthetic sample data only.
- Use separate demo accounts with non-sensitive credentials.
- Do not use employer/customer data or screenshots without authorization.
- Do not publish secrets in GitHub history.
