# Jiva – Testing Strategy

## 1. Test layers

### Domain/application unit tests
Test business rules without a database:
- Task status transition rules
- Project date validation
- Approval step progression
- Approver identity validation
- Ticket status transitions
- Role/resource authorization decisions
- Date and time boundary rules

### Infrastructure/database tests
Use a real PostgreSQL instance for relational behavior. Do not rely exclusively on EF Core InMemory because it does not reproduce PostgreSQL constraints, SQL translation or transaction behavior.

Test:
- EF Core migrations against an empty database
- Unique constraints and foreign keys
- Transaction rollback
- Concurrent approval decisions
- Optimistic concurrency conflicts
- Query filters and pagination

### API integration tests
Use `WebApplicationFactory` and a dedicated test PostgreSQL database/container:
- Authentication required
- Role policy behavior
- Resource-level authorization
- DTO validation
- Problem Details responses
- Project/task lifecycle
- Ticket internal-note privacy
- Approval decision concurrency
- User notification isolation

### Angular tests
- Component rendering and interaction
- Form validation
- API service behavior
- Route guard behavior (UX only)
- Error/loading/empty states
- Role-based navigation display
- Critical feature workflows

## 2. Minimum test matrix

| Area | Required tests |
|---|---|
| Authentication | valid login, invalid login, inactive account, refresh rotation, logout |
| Projects | create, duplicate code, member access, non-member denial, archive |
| Tasks | create, task key assignment, invalid assignee, invalid transition (`InReview` rules), stale version, comments |
| Git & Webhooks | repo connection auth, encrypted secrets, branch creation, HMAC-SHA256 signature verification, DeliveryId idempotency, PR auto-link & auto-transition |
| Tickets | requester visibility, agent assignment, internal-note privacy, status transitions |
| Approvals | correct approver, wrong approver, double decision, rejection, all steps approved |
| Notifications | own list, cannot access another user's notification |
| Admin | role changes restricted, audit record generated |
| Database | clean migration, FK/unique constraints, transaction rollback |

## 3. Test data
- Use deterministic synthetic data.
- Create test users for each role.
- Keep tests isolated and repeatable.
- Reset the database between integration test suites or use isolated schemas/databases.
- Never use production data.

## 4. Quality gates
A phase is not complete until:
- Build succeeds.
- Relevant unit tests pass.
- Relevant integration tests pass.
- Frontend tests pass for changed UI.
- No unexplained warnings or skipped tests remain.
- Manual smoke test is recorded.
- README/docs reflect actual behavior.

## 5. CI pipeline
Suggested sequence:
1. Checkout
2. Install pinned .NET SDK
3. Restore
4. Build Release
5. Run unit tests
6. Start PostgreSQL service/container
7. Run integration tests
8. Install frontend dependencies using lockfile
9. Run frontend lint/test/build
10. Publish test results

Do not make CI green by silently skipping failing tests.
