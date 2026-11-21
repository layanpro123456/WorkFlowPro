# Definition of Done

A feature is complete only when all applicable conditions are met:

## Requirements
- [ ] User story and acceptance criteria are clear.
- [ ] Authorization rules are explicitly defined.
- [ ] Validation and failure cases are defined.

## Backend
- [ ] Domain invariants are enforced.
- [ ] Application use case is implemented.
- [ ] API DTOs are explicit; entities are not exposed.
- [ ] Async I/O and CancellationToken are used appropriately.
- [ ] Authorization is enforced server-side.
- [ ] Errors use consistent Problem Details responses.
- [ ] Database constraints/indexes are considered.
- [ ] Transactions/concurrency are handled where needed.

## Frontend
- [ ] Loading, empty, success and error states exist.
- [ ] Forms validate input and display actionable errors.
- [ ] Responsive and keyboard-accessible behavior is checked.
- [ ] API calls are isolated in services.
- [ ] No security decision relies only on client-side checks.

## Quality
- [ ] Unit tests pass.
- [ ] Integration tests pass for persistence/API behavior.
- [ ] Frontend tests pass for changed behavior.
- [ ] Build and lint pass.
- [ ] No secrets or sensitive sample data are committed.
- [ ] Manual smoke test is recorded.

## Documentation
- [ ] API/docs updated.
- [ ] README accurately describes implementation status.
- [ ] Known limitations are recorded.
- [ ] No feature is marked complete without evidence.
