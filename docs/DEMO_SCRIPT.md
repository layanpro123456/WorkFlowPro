# Jiva — Complete Feature & Flow Demo Script

This guide provides **two complementary ways** to demo every feature, workflow, and security guardrail in **Jiva** *(codebase `WorkFlowPro`)*:

1. **Part A — Live Presenter UI Walkthrough (~12–15 minutes)**: A scene-by-scene spoken + screen-click script covering all 8 modules and multi-role handoffs in the Angular web app.
2. **Part B — Automated Runnable Demo Script (`scripts/demo_all_features.py`)**: A zero-dependency Python 3 script that executes every end-to-end flow and negative security test against the live API (supports `--pause` for live narration alongside a terminal).

---

## 1. Pre-Demo Environment Setup

### Option 1: Docker Compose (Full Stack: PostgreSQL + API + Angular/Nginx)
```bash
docker compose up --build -d
```
- **Web App:** `http://localhost`
- **API / Swagger:** `http://localhost:8080/health/ready`
- **Demo Seeding:** Enabled by default (`SEED_DEMO_DATA=true` in `docker-compose.yml`).

### Option 2: Local Development (.NET 9 + SQLite + Angular Dev Server)
In Terminal 1 (API with demo seeding enabled):
```powershell
$env:SeedDemoData = "true"
dotnet run --project backend/src/WorkFlowPro.Api
```
*(API starts at `http://localhost:5225`, Scalar API docs at `http://localhost:5225/scalar/v1`.)*

In Terminal 2 (optional Angular UI if running outside Docker):
```powershell
cd frontend/workflowpro-web
npm start
```

---

## 2. Verified Seeded Demo Accounts

> **Note:** These credentials come directly from `ApplicationDbContextInitialiser.cs`.

| Persona | Role | Email | Password |
|---|---|---|---|
| **System Administrator** | `Administrator` | `admin@workflowpro.local` | `Admin@123456` |
| **Alex Mercer** | `Manager` | `manager@workflowpro.local` | `Password@123` |
| **Sarah Connor** | `SupportAgent` | `agent@workflowpro.local` | `Password@123` |
| **David Chen** | `Employee` | `employee@workflowpro.local` | `Password@123` |
| **Elena Rostova** | `Employee` | `employee2@workflowpro.local` | `Password@123` |

*(Pro tip: Open two browser profiles or one regular window + one Incognito window to demonstrate real-time cross-user handoffs without signing in/out repeatedly—especially since `/api/v1/auth/*` enforces a 15 req/min `AuthPolicy` rate limit.)*

---

## Part A — Live Presenter UI Walkthrough

### Scene 1: Authentication, RBAC & Role-Tailored Dashboard (1.5 min)
1. **Navigate to `/login`**:
   - Point out the **"Use Demo Account"** quick-fill button (`admin@workflowpro.local` / `Admin@123456`), then instead sign in first as **Alex Mercer (Manager)** (`manager@workflowpro.local` / `Password@123`) to start the operational story.
2. **Arrive at `/dashboard`**:
   - **Show:** Welcome banner with role pill (`Manager`), KPI cards (**Assigned Tasks**, **Overdue Tasks**, **Open Tickets**, **Pending Approvals**, **Managed Projects**, **Reports**), and the **⚡ Approvals Requiring Your Review** table (pre-seeded `#1001`).
   - **Say:** *"Every user lands on a caller-authorized KPI dashboard. Notice the top navigation bar does NOT show Administration because Alex has the `Manager` role, not `Administrator`."*

---

### Scene 2: Project Lifecycle & Membership Governance (2 min)
1. **Click `Projects` (`/projects`)**:
   - Show search bar and status filter pills (`Active`, `OnHold`, `Completed`, `Archived`).
   - Click **`+ New Project`**, enter:
     - **Project Code:** `WFP-Q4`
     - **Name:** `Zero-Trust Identity Rollout`
     - **Start / Target Date:** Today → +30 days
   - Submit and click **`View Details →`**.
2. **Project Detail (`/projects/:id`)**:
   - Under **Project Team**, click **`+ Add Member`**.
   - Add **David Chen (`employee@workflowpro.local`)** as `Member` and **Elena Rostova (`employee2@workflowpro.local`)** as `Member`.
   - **Callout:** *"Tasks can only be assigned to active members of this project—preventing orphaned work. And any non-member querying this project URL receives `404 Not Found` instead of `403` to prevent ID enumeration."*

---

### Scene 3: Interactive Kanban Board, State Machine & Concurrency (2.5 min)
1. **Click `View Tasks` (`/projects/:id/tasks`)**:
   - Switch between **`📋 Kanban Board`** and **`☰ List View`** using the header toggle.
   - Click **`+ New Task`**:
     - **Title:** `Configure SSO SAML IdP Metadata`
     - **Priority:** `High`
     - **Assignee:** `David Chen`
     - **Status:** `Backlog`
     - **Estimate:** `180` minutes
2. **Demonstrate State Machine Flow**:
   - Use the card's **Move to** dropdown (or click the task title to open `/tasks/:id`):
     - `Backlog` → `ToDo` → `InProgress` → `Blocked` → `InProgress` → `Done`
   - **Open Task Detail (`/tasks/:id`)**:
     - Point out **`Available Transitions`** buttons (only valid next states are enabled per `docs/TASK_TRANSITIONS.md`; invalid jumps like `Backlog → Done` are blocked by the domain state machine with `HTTP 400`).
     - Point out **`Version`** badge (e.g., `Version 6`)—explain that every update passes `version` for **optimistic concurrency control** (`HTTP 409 Conflict` on stale edits).
     - Post a comment under **Discussion** and scroll the **Status History** timeline in the right sidebar.

---

### Scene 3b: Git, Branch Creation & Pull Request Sync (2 min)
1. **Project Detail (`/projects/:id`) — Connect GitHub**:
   - Under **Git Repositories & Pull Requests**, click **`+ Connect GitHub Repo`**, enter owner/repo, default branch, optional PAT, and toggle **Auto-link**, **Auto-transition**, and **InReview column**.
   - Copy the generated **Webhook Secret** and **Webhook URL** (`/api/v1/webhooks/git/github/{repoId}`), or click **Test** / **Sync PRs** to pull open/merged PRs on demand.
2. **Kanban Board (`/tasks`) — Create Branch & Track PRs**:
   - Point out sequential card keys (`PRJ-ALPHA-2`, `WFP-Q4-1`), PR pills (`PR #42 Open ✓`), and the **`InReview`** column.
   - Click **`🌿+`** on a card to create `feat/WFP-Q4-1-configure-sso` directly on GitHub—the card automatically transitions to `InProgress`.
   - When a PR referencing `WFP-Q4-1` is opened (or synced via webhook), the card automatically moves to `InReview`, displays CI check & review badges, and transitions to `Done` upon merge.

---

### Scene 4: Service Desk & Internal Notes Privacy Boundary (2.5 min)
1. **Sign out → Sign in as David Chen (`employee@workflowpro.local` / `Password@123`)**:
   - Click **`Service Desk` (`/tickets`)** → **`+ Submit Ticket`**:
     - **Subject:** `Intermittent 502 on SSO callback`
     - **Category:** `Technical`, **Priority:** `High`, **Related Project:** `WFP-Q4`
   - Note the human-friendly sequential ID (`#1003`).
2. **Switch to Sarah Connor (`agent@workflowpro.local` / `Password@123`)**:
   - Open **`Service Desk` (`/tickets`)** — show the **`All Tickets`** vs **`My Submitted Tickets`** tabs (visible to agents/managers).
   - Click the new ticket to open `/tickets/:id`:
     - Under **Assignee**, assign to **Sarah Connor**.
     - Click **`Move to InProgress`**.
     - Type a **public reply** (*"Inspecting gateway logs—could you share a HAR trace?"*) and click **`Send Reply`**.
     - Check **`🔒 Post as internal note (hidden from customer)`**, type *"INTERNAL: Looks like SAML header overflowing nginx proxy_buffer_size"*, and click **`Post Internal Note`**.
3. **Verify Privacy Boundary (Switch back to David Chen)**:
   - Open the same ticket as David Chen:
   - **Highlight:** The internal staff note is **completely absent** (filtered out at the database projection layer, not merely hidden in CSS!).
   - Switch back to Sarah Connor and walk through: `WaitingForRequester` → `InProgress` → `Resolved` → `Closed`.

---

### Scene 5: Sequential Multi-Step Approval Engine (3 min)
1. **As David Chen (`employee@workflowpro.local`), click `Approvals` (`/approvals`)**:
   - Show tabs: **`⚡ My Inbox`**, **`My Submitted Requests`**, and (for managers/admins) **`All Organizational Requests`**.
   - Click **`+ Submit Request`**:
     - Select **`Capital Expenditure Approval`** (2 steps: *1. Engineering Lead Review* → *2. Executive Signoff*).
     - **Title:** `Provision HA Redis Cluster`
     - **Description:** `Needed for distributed rate limiting across API replicas.`
     - Submit and open the request detail (`/approvals/:id`) to show the **Visual Progress Stepper** (`Step 1` active, assigned to Alex Mercer; `Step 2` pending, assigned to System Administrator).
   - **Callout:** *"Approvers are resolved and snapshotted at submission time—so editing the workflow template later never corrupts in-flight approvals."*
2. **Step 1 Review — Sign in as Alex Mercer (`manager@workflowpro.local`)**:
   - Check the **🔔 Notification Bell** in the header — point out the unread **`Action Required`** notification and click it to jump straight to `/approvals/:id`.
   - Enter comment *"Architecture approved"* and click **`✓ Approve Step`**.
   - **Highlight:** Step 1 turns green (`✓`), and Step 2 (**Executive Signoff**) becomes active for `System Administrator`.
3. **Step 2 Signoff — Sign in as System Administrator (`admin@workflowpro.local`)**:
   - Open **`Approvals` → `⚡ My Inbox`**, click **`Review`**, add *"Budget cleared"*, and click **`✓ Approve Step`**.
   - Request transitions to terminal **`Approved`** state; requester receives **`Approval Request Approved`** notification.
4. *(Optional bonus)* **Terminal Rejection**: Submit a second request as Employee and click **`✕ Reject Request`** at Step 1 as Manager—show that the request immediately becomes **`Rejected`** and Step 2 is automatically marked **`Skipped`**.

---

### Scene 6: In-App Notifications, Reports & RFC 4180 CSV Export (1.5 min)
1. **Notification Center**:
   - Click the **🔔 Bell** in the header, show unread badge, click a notification to navigate directly to the linked entity, or click **`Mark all as read`**.
2. **Reports (`/reports`)**:
   - Open **`Reports`**, view KPIs (`Total Tasks`, `Completed`, `Completion Rate %`, `Overdue`) and **Status / Priority Breakdown** pill clouds.
   - Switch to **`🎫 Service Desk Report`** tab for category/priority/status distributions.
   - Filter by date range and click **`📥 Export CSV`** to download an RFC 4180 compliant spreadsheet export.

---

### Scene 7: Administration, Session Revocation & Audit Log Explorer (2 min)
1. **Click `Administration` (`/admin`) as `admin@workflowpro.local`**:
   - **User Management Tab:**
     - Click **`➕ Create User`** to provision a contractor (`contractor@workflowpro.local`).
     - Click **`Edit Roles`** to grant `SupportAgent`.
     - Click **`Deactivate`** — explain that deactivation immediately revokes all active refresh tokens in PostgreSQL/SQLite.
     - Mention **Sole-Administrator Protection**: attempting to deactivate the last active admin is rejected server-side.
   - **📜 Audit Log Explorer Tab:**
     - Filter by Action / Entity Type (`User`, `Project`, etc.) to show the append-only audit trail with actor, timestamp, `X-Correlation-Id`, and JSON metadata diffs.

---

## Part B — Automated End-to-End Demo Script

Run the automated script to execute **all 8 scenes** (including all negative security checks—`400`, `401`, `403`, `404`, `409`) in ~2 seconds, or in interactive `--pause` mode:

```bash
# Auto-detects http://localhost:5225, :8080, or :80
python scripts/demo_all_features.py

# Interactive presenter mode (press Enter between scenes)
python scripts/demo_all_features.py --pause

# Explicit URL + save exported CSV reports to disk
python scripts/demo_all_features.py --base-url http://localhost:8080 --save-csv-dir ./demo-exports
```

### What `scripts/demo_all_features.py` Verifies
| Scene | Features & Flows Covered | Guardrails Verified |
|---|---|---|
| **1. Auth & Health** | `/health/live`, `/health/ready`, `/api/v1/health`, login for 5 roles, `/auth/me`, `/auth/refresh` rotation | Security headers (`X-Frame-Options`, `X-Content-Type-Options`), `AuthPolicy` token budget |
| **2. Projects** | Create project, add members, update metadata, archive project | Employee create -> `403`, Non-member read -> `404` concealment |
| **3. Kanban Tasks** | Create task, `Backlog → ToDo → InProgress → Blocked → InProgress → Done`, comments, history | Invalid transition `Backlog → Done` -> `400`, Stale `version` -> `409 Conflict` |
| **4. Service Desk** | Create ticket `#100x`, list agents, assign, public reply + internal note, `WaitingForRequester → Resolved → Closed` | Requester internal note -> `403`, Requester GET messages hides internal notes |
| **5. Approvals** | Create 2-step workflow, submit request, inbox query, Step 1 + Step 2 approvals, rejection flow, cancel flow | Out-of-turn approver -> `403`, Double-decision/stale version -> `403`/`409`, Reject skips downstream steps |
| **6. Notifications** | List unread, unread count, mark one read, mark all read | Cross-user mark-read -> `404` isolation |
| **7. Dashboard & Reports** | Caller-specific `/dashboard/summary`, `/reports/tasks`, `/reports/tickets`, `/reports/projects/:id`, CSV export | Validated non-empty RFC 4180 CSV bytes |
| **8. Admin & Audit** | Create user, update roles, deactivate user, query `/admin/audit-logs` | Non-admin -> `403`, Deactivation revokes refresh token (`401`), Self/sole-admin deactivation blocked (`400`/`409`) |
