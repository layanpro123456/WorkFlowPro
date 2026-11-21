# Prompt – Phase 4: Service Desk

Implement ticket workflow:
- Requester creates and views own tickets.
- Support agents/managers/admins list and assign tickets.
- Statuses: New, Assigned, InProgress, WaitingForRequester, Resolved, Closed.
- Requester replies and agent replies.
- Agent-only internal notes.
- Assignment/status history and audit events.
- Angular requester and support-agent screens.

Critical privacy rule: requester API responses must never include internal notes, even if the frontend hides them. Use separate query/DTO behavior and test it.

Add authorization, state transition, assignment and internal-note privacy integration tests against PostgreSQL.
