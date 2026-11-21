# WorkFlowPro – PostgreSQL Database Design

## 1. Conventions

- Primary keys: UUID (`Guid` in .NET).
- Timestamps: `timestamp with time zone`, stored as UTC.
- Text: `text` or bounded `varchar` where a real limit exists.
- Money/estimates: `numeric`, not float/double.
- Enum-like workflow states: initially map .NET enums to strings or constrained small integers consistently; prefer strings for readability unless database constraints and migrations are carefully managed.
- Add `created_at`, `updated_at` where appropriate.
- Use restrictive FK behavior for historical records; avoid cascading deletion of business history.
- EF Core migrations are the schema source of truth.

## 2. Main entities and relationships

```text
AspNetUsers (Identity)
   | 1
   |----< ProjectMembers >---- 1 Projects
   |                              |
   |                              |----< Tasks
   |                              |       |----< TaskComments
   |                              |       |----< TaskStatusHistory
   |                              |
   |                              |----< ProjectActivity (optional)

AspNetUsers
   |----< SupportTickets (requester)
   |----< SupportTickets (assignee, optional)
   |----< TicketMessages
   |----< Notifications
   |----< RefreshTokens

WorkflowDefinitions
   |----< WorkflowSteps
   |
ApprovalRequests
   |----< ApprovalDecisions
   |----< ApprovalRequestSteps (snapshot of steps/approvers)

AspNetUsers
   |----< AuditLogs (actor)
```

## 3. Table catalogue

### Identity
Use ASP.NET Core Identity tables (AspNetUsers, AspNetRoles, AspNetUserRoles, claims, logins, tokens). Extend the user with display name, is_active and created_at. Avoid duplicating role membership in a custom table.

### Projects
- id UUID PK
- project_code varchar(30) unique
- name varchar(150)
- description text nullable
- status varchar(20)
- start_date date nullable
- target_date date nullable
- created_by UUID FK AspNetUsers
- created_at timestamptz
- updated_at timestamptz
- archived_at timestamptz nullable

Indexes: unique(project_code), status, created_by.

### ProjectMembers
- project_id UUID FK Projects
- user_id UUID FK AspNetUsers
- project_role varchar(20) (Owner, Manager, Member)
- joined_at timestamptz
- PK(project_id, user_id)

Indexes: user_id, project_id/project_role.

### Tasks
- id UUID PK
- project_id UUID FK Projects
- title varchar(200)
- description text nullable
- status varchar(20)
- priority varchar(20)
- assignee_id UUID FK AspNetUsers nullable
- created_by UUID FK AspNetUsers
- due_at timestamptz nullable
- estimate_minutes integer nullable
- version integer not null default 1
- created_at, updated_at timestamptz

Indexes: project_id/status, assignee_id/status, due_at, created_at.
Constraints: estimate_minutes >= 0; valid status/priority via application and optionally DB check constraints.

### TaskComments
- id UUID PK
- task_id UUID FK Tasks
- author_id UUID FK AspNetUsers
- body text
- created_at timestamptz

Index: task_id/created_at.

### TaskStatusHistory
- id UUID PK
- task_id UUID FK Tasks
- from_status varchar(20) nullable
- to_status varchar(20)
- changed_by UUID FK AspNetUsers
- changed_at timestamptz
- comment text nullable

Index: task_id/changed_at.

### SupportTickets
- id UUID PK
- ticket_number bigint generated/assigned uniquely
- requester_id UUID FK AspNetUsers
- assignee_id UUID FK AspNetUsers nullable
- related_project_id UUID FK Projects nullable
- subject varchar(200)
- description text
- category varchar(50)
- priority varchar(20)
- status varchar(30)
- resolved_at timestamptz nullable
- closed_at timestamptz nullable
- created_at, updated_at timestamptz

Indexes: requester_id/status, assignee_id/status, status/priority, created_at.

### TicketMessages
- id UUID PK
- ticket_id UUID FK SupportTickets
- author_id UUID FK AspNetUsers
- body text
- is_internal_note boolean
- created_at timestamptz

Index: ticket_id/created_at. Requester-facing queries must filter `is_internal_note = false`.

### WorkflowDefinitions
- id UUID PK
- name varchar(150)
- description text nullable
- is_active boolean
- created_by UUID FK AspNetUsers
- created_at, updated_at timestamptz

### WorkflowSteps
- id UUID PK
- workflow_definition_id UUID FK WorkflowDefinitions
- step_order integer
- approver_role varchar(50) nullable
- approver_user_id UUID FK AspNetUsers nullable
- name varchar(150)
- unique(workflow_definition_id, step_order)
- Validate exactly one approver selector is configured.

### ApprovalRequests
- id UUID PK
- request_number bigint unique
- workflow_definition_id UUID FK WorkflowDefinitions
- requester_id UUID FK AspNetUsers
- title varchar(200)
- description text
- status varchar(20) (Pending, Approved, Rejected, Cancelled)
- current_step_order integer nullable
- submitted_at timestamptz
- completed_at timestamptz nullable
- version integer not null default 1

### ApprovalRequestSteps
Snapshot the configured steps and resolved approver at submission time:
- id UUID PK
- approval_request_id UUID FK ApprovalRequests
- step_order integer
- name varchar(150)
- approver_user_id UUID FK AspNetUsers
- status varchar(20) (Pending, Approved, Rejected, Skipped)
- decided_at timestamptz nullable
- unique(approval_request_id, step_order)

### ApprovalDecisions
- id UUID PK
- approval_request_step_id UUID FK ApprovalRequestSteps
- approver_id UUID FK AspNetUsers
- decision varchar(20)
- comment text nullable
- decided_at timestamptz
- unique(approval_request_step_id)

### Notifications
- id UUID PK
- recipient_id UUID FK AspNetUsers
- type varchar(60)
- title varchar(200)
- message text
- entity_type varchar(50) nullable
- entity_id UUID nullable
- read_at timestamptz nullable
- created_at timestamptz

Index: recipient_id/read_at/created_at.

### AuditLogs
- id UUID PK
- actor_id UUID FK AspNetUsers nullable (null for system events)
- action varchar(100)
- entity_type varchar(80)
- entity_id UUID nullable
- occurred_at timestamptz
- correlation_id varchar(100) nullable
- metadata jsonb nullable (allowlisted safe fields only)

Indexes: occurred_at, actor_id/occurred_at, entity_type/entity_id.

### RefreshTokens
- id UUID PK
- user_id UUID FK AspNetUsers
- token_hash varchar(256) unique
- expires_at timestamptz
- created_at timestamptz
- revoked_at timestamptz nullable
- replaced_by_token_id UUID nullable
- device_label varchar(100) nullable

Store only hashes of refresh tokens. Define rotation/reuse detection behavior in implementation.

## 4. Important constraints and query design

- Every FK must have an explicit delete behavior.
- Project membership is unique per user/project.
- Project code and ticket/request numbers are unique.
- Approval decisions are unique per step.
- Add check constraints for positive page-independent numeric fields and valid version values.
- Avoid unbounded `Include` graphs; use projections.
- Paginate all potentially large collections.
- Use `AsNoTracking` for read-only queries.
- Add migrations and inspect generated SQL for important schema changes.

## 5. ERD generation

Create `docs/erd.mmd` using Mermaid ER diagram syntax once entity names and actual EF relationships are implemented. Do not represent proposed relationships as implemented schema until migrations exist.
