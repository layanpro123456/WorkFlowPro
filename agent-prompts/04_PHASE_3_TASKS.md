# Prompt – Phase 3: Tasks

Implement tasks end-to-end for authorized project members.

Required:
- Create/list/detail/update tasks.
- Assign only active project members.
- Statuses: Backlog, ToDo, InProgress, Blocked, Done, Cancelled.
- Priorities: Low, Normal, High, Urgent.
- Comments and immutable status history.
- Status transition policy.
- Optimistic concurrency using explicit version field and 409 conflicts.
- Angular task list, detail, create/edit form, filters and Kanban view.

Do not invent transition rules: define a clear transition matrix consistent with requirements, document it, and add tests. Avoid adding drag-and-drop unless keyboard-accessible alternatives exist.

Test invalid assignee, unauthorized access, invalid transition, stale version and history creation using PostgreSQL integration tests.
