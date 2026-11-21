# Prompt – Phase 2: Projects and Membership

Read master instructions and FR-02, architecture, database, API and testing docs.

Implement projects end-to-end:
- Project create/list/detail/update/archive.
- Unique project code.
- Project membership with Owner/Manager/Member.
- Add/remove members with authorization.
- Project-level access policy and resource authorization.
- Pagination, search and status filter.
- Angular project list, details, edit/create forms and member management.

Rules:
- Do not physically delete projects with historical tasks.
- Only authorized project managers/admins can manage members.
- Non-members must not read restricted project information.
- Validate date ranges.
- Use DTOs and EF Core configurations/migrations.

Add PostgreSQL integration tests for duplicate project code, membership, access denial and archive behavior. Complete the phase exit criteria before proceeding.
