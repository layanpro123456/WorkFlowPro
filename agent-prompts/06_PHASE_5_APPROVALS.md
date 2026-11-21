# Prompt – Phase 5: Sequential Approval Workflows

Implement simple sequential approvals, not a general BPMN engine.

Required:
- Workflow definition with ordered steps.
- Each step has exactly one approver selector: specific user or role.
- On submission, snapshot steps and resolve role approvers to a concrete approver according to a documented rule.
- Approval request with current step and version.
- Only assigned current approver can approve/reject.
- Reject ends request; all approved steps complete request.
- Decision comments and immutable history.
- Transactional decision operation and unique constraint preventing duplicate decision.
- Angular workflow setup, request submission, approval inbox and detail/history screens.

If a role maps to multiple users, do not silently select one. Define and document a deterministic assignment policy or require a specific approver for MVP. Ask before implementing ambiguous business policy.

Test wrong approver, double decision, rejection, all-step completion and concurrent decisions against PostgreSQL.
