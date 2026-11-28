#!/usr/bin/env python3
"""
WorkFlowPro End-to-End Interactive Feature & Flow Demo Script
==============================================================
Zero-dependency Python 3 script (stdlib only) that exercises all 8 WorkFlowPro
functional modules, end-to-end business flows, and security/domain guardrails
against a running WorkFlowPro API instance.

Usage:
  python scripts/demo_all_features.py
  python scripts/demo_all_features.py --base-url http://localhost:5225
  python scripts/demo_all_features.py --base-url http://localhost:8080 --pause
  python scripts/demo_all_features.py --save-csv-dir ./demo-exports
"""

from __future__ import annotations

import argparse
import http.cookiejar
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Any, Optional


# ---------------------------------------------------------------------------
# Terminal formatting helpers (safe fallback on non-ANSI terminals)
# ---------------------------------------------------------------------------
def _supports_color() -> bool:
    if os.environ.get("NO_COLOR"):
        return False
    return sys.stdout.isatty() or os.environ.get("WT_SESSION") is not None


USE_COLOR = _supports_color()


def c(code: str, text: str) -> str:
    if not USE_COLOR:
        return text
    return f"\033[{code}m{text}\033[0m"


def banner(title: str) -> None:
    line = "=" * 78
    print(f"\n{c('1;36', line)}")
    print(c("1;36", f"  {title}"))
    print(f"{c('1;36', line)}")


def scene(num: int, title: str, subtitle: str = "") -> None:
    print(f"\n{c('1;34', f'>>> SCENE {num}: {title}')}")
    if subtitle:
        print(f"    {c('2', subtitle)}")


def step(msg: str) -> None:
    print(f"  {c('33', '•')} {msg}")


def ok(msg: str) -> None:
    print(f"    {c('1;32', '✓ PASS')} {msg}")


def info(msg: str) -> None:
    print(f"    {c('36', 'ℹ INFO')} {msg}")


def warn(msg: str) -> None:
    print(f"    {c('1;33', '! NOTE')} {msg}")


def fail(msg: str) -> None:
    print(f"    {c('1;31', '✗ FAIL')} {msg}")


# ---------------------------------------------------------------------------
# HTTP Client with CookieJar (for HttpOnly refresh cookies) & Bearer JWT
# ---------------------------------------------------------------------------
@dataclass
class HttpResponse:
    status: int
    headers: dict[str, str]
    body_bytes: bytes

    @property
    def text(self) -> str:
        return self.body_bytes.decode("utf-8", errors="replace")

    def json(self) -> Any:
        if not self.body_bytes:
            return None
        return json.loads(self.text)


class ApiClient:
    def __init__(self, base_url: str, timeout: int = 15) -> None:
        self.base_url = base_url.rstrip("/")
        self.timeout = timeout
        self.cookie_jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.cookie_jar)
        )
        self.tokens: dict[str, str] = {}
        self.profiles: dict[str, dict[str, Any]] = {}

    def request(
        self,
        method: str,
        path: str,
        *,
        token: Optional[str] = None,
        json_body: Optional[Any] = None,
        query: Optional[dict[str, Any]] = None,
        headers: Optional[dict[str, str]] = None,
    ) -> HttpResponse:
        url = f"{self.base_url}{path}"
        if query:
            clean_q = {k: v for k, v in query.items() if v is not None}
            if clean_q:
                url = f"{url}?{urllib.parse.urlencode(clean_q)}"

        req_headers = {"Accept": "application/json"}
        if headers:
            req_headers.update(headers)
        if token:
            req_headers["Authorization"] = f"Bearer {token}"

        data: Optional[bytes] = None
        if json_body is not None:
            req_headers["Content-Type"] = "application/json"
            data = json.dumps(json_body).encode("utf-8")

        req = urllib.request.Request(url, data=data, headers=req_headers, method=method)
        try:
            with self.opener.open(req, timeout=self.timeout) as resp:
                return HttpResponse(
                    status=resp.status,
                    headers={k.lower(): v for k, v in resp.headers.items()},
                    body_bytes=resp.read(),
                )
        except urllib.error.HTTPError as err:
            return HttpResponse(
                status=err.code,
                headers={k.lower(): v for k, v in err.headers.items()},
                body_bytes=err.read(),
            )

    def login(self, alias: str, email: str, password: str) -> dict[str, Any]:
        resp = self.request(
            "POST",
            "/api/v1/auth/login",
            json_body={"email": email, "password": password},
            headers={"User-Agent": f"WorkFlowPro-Demo/{alias}"},
        )
        if resp.status != 200:
            raise RuntimeError(
                f"Login failed for {email} ({resp.status}): {resp.text}"
            )
        payload = resp.json()
        self.tokens[alias] = payload["accessToken"]
        self.profiles[alias] = payload["user"]
        return payload

    def token(self, alias: str) -> str:
        return self.tokens[alias]

    def user_id(self, alias: str) -> str:
        return self.profiles[alias]["id"]


def assert_status(resp: HttpResponse, expected: int | tuple[int, ...], context: str) -> None:
    allowed = (expected,) if isinstance(expected, int) else expected
    if resp.status not in allowed:
        raise AssertionError(
            f"{context}: expected HTTP {allowed}, got {resp.status}. Body: {resp.text}"
        )


def pause_if_requested(enabled: bool, next_label: str) -> None:
    if enabled:
        input(c("2", f"\n  [Press Enter to continue to {next_label}...]"))


def detect_base_url(explicit_url: Optional[str]) -> str:
    if explicit_url:
        return explicit_url.rstrip("/")

    candidates = [
        "http://localhost:5225",  # Local .NET launchSettings
        "http://localhost:8080",  # Docker Compose API port
        "http://localhost",       # Docker Compose Nginx reverse proxy
    ]
    for candidate in candidates:
        try:
            req = urllib.request.Request(f"{candidate}/health/live", method="GET")
            with urllib.request.urlopen(req, timeout=2) as resp:
                if resp.status == 200:
                    return candidate
        except Exception:
            continue

    raise RuntimeError(
        "Could not reach WorkFlowPro on localhost:5225, localhost:8080, or localhost:80.\n"
        "Start the stack first (e.g., `SeedDemoData=true dotnet run --project backend/src/WorkFlowPro.Api` "
        "or `docker compose up --build -d`) or pass --base-url."
    )


# ---------------------------------------------------------------------------
# Main Demo Flow
# ---------------------------------------------------------------------------
def run_demo(args: argparse.Namespace) -> int:
    run_tag = datetime.now(timezone.utc).strftime("%H%M%S")
    base_url = detect_base_url(args.base_url)
    client = ApiClient(base_url)

    banner(f"WorkFlowPro Full-Feature Live Demo  |  Target: {base_url}  |  Run Tag: {run_tag}")

    # =========================================================================
    # SCENE 1: Health Probes, Security Headers & Multi-Role Authentication
    # =========================================================================
    scene(1, "Health Probes, Security Headers & Multi-Role Auth",
          "Verifies liveness/readiness, security headers, and authenticates all 5 demo personas.")

    for probe in ("/health/live", "/health/ready", "/api/v1/health"):
        r = client.request("GET", probe)
        assert_status(r, 200, f"Health probe {probe}")
        ok(f"GET {probe} -> {r.status} ({r.text.strip()[:80]})")

    # Verify security headers on API response
    sec_headers = r.headers
    info(
        "Security headers present: "
        f"X-Content-Type-Options={sec_headers.get('x-content-type-options', 'missing')}, "
        f"X-Frame-Options={sec_headers.get('x-frame-options', 'missing')}, "
        f"X-Correlation-Id={sec_headers.get('x-correlation-id', 'n/a')}"
    )

    step("Authenticating all 5 seeded enterprise personas (tokens cached to respect AuthPolicy 15 req/min)...")
    personas = [
        ("admin", args.admin_email, args.admin_password),
        ("manager", "manager@workflowpro.local", args.demo_password),
        ("agent", "agent@workflowpro.local", args.demo_password),
        ("employee", "employee@workflowpro.local", args.demo_password),
        ("employee2", "employee2@workflowpro.local", args.demo_password),
    ]
    for alias, email, pwd in personas:
        auth_data = client.login(alias, email, pwd)
        roles = ", ".join(auth_data["user"]["roles"])
        ok(f"Logged in {alias:9s} -> {auth_data['user']['displayName']} [{roles}]")

    step("Verifying GET /api/v1/auth/me and HttpOnly refresh token rotation...")
    me_resp = client.request("GET", "/api/v1/auth/me", token=client.token("employee"))
    assert_status(me_resp, 200, "GET /api/v1/auth/me")
    ok(f"GET /api/v1/auth/me confirmed identity: {me_resp.json()['email']}")

    # Refresh rotation uses cookie jar (employee2 logged in last, so cookie holds employee2's refresh token)
    refresh_resp = client.request("POST", "/api/v1/auth/refresh", json_body={})
    assert_status(refresh_resp, 200, "Refresh token rotation")
    client.tokens["employee2"] = refresh_resp.json()["accessToken"]
    ok("POST /api/v1/auth/refresh rotated HttpOnly cookie & issued fresh JWT for employee2")

    pause_if_requested(args.pause, "Scene 2 (Projects & Resource Concealment)")

    # =========================================================================
    # SCENE 2: Project Lifecycle, Membership Governance & Resource Concealment
    # =========================================================================
    scene(2, "Project Lifecycle, Membership Governance & Resource Concealment",
          "Creates a project, enforces RBAC + 404 concealment for non-members, adds members, and updates metadata.")

    proj_code = f"DEMO-{run_tag}"
    today = datetime.now(timezone.utc).date()
    project_payload = {
        "projectCode": proj_code,
        "name": f"Q4 Zero-Trust Rollout ({run_tag})",
        "description": "End-to-end demonstration project created by demo_all_features.py",
        "startDate": today.isoformat(),
        "targetDate": (today + timedelta(days=30)).isoformat(),
    }

    step("Negative RBAC test: Employee tries to create a project (expect 403 Forbidden)...")
    r_emp_proj = client.request("POST", "/api/v1/projects", token=client.token("employee"), json_body=project_payload)
    assert_status(r_emp_proj, 403, "Employee creating project")
    ok("Employee project creation blocked with HTTP 403 Forbidden")

    step(f"Manager creates project {proj_code}...")
    r_proj = client.request("POST", "/api/v1/projects", token=client.token("manager"), json_body=project_payload)
    assert_status(r_proj, 201, "Manager creating project")
    project = r_proj.json()
    project_id = project["id"]
    ok(f"Created Project [{project['projectCode']}] {project['name']} (id={project_id})")

    step("Security check: Non-member Employee2 queries private project (expect 404 Not Found concealment)...")
    r_conceal = client.request("GET", f"/api/v1/projects/{project_id}", token=client.token("employee2"))
    assert_status(r_conceal, 404, "Non-member project concealment")
    ok("Non-member query returned HTTP 404 Not Found (prevents resource ID enumeration)")

    step("Manager adds Employee 1 and Employee 2 as project Members...")
    for alias in ("employee", "employee2"):
        r_mem = client.request(
            "POST",
            f"/api/v1/projects/{project_id}/members",
            token=client.token("manager"),
            json_body={"userId": client.user_id(alias), "projectRole": "Member"},
        )
        assert_status(r_mem, 201, f"Add {alias} to project")
        ok(f"Added {client.profiles[alias]['displayName']} as Member")

    r_emp2_ok = client.request("GET", f"/api/v1/projects/{project_id}", token=client.token("employee2"))
    assert_status(r_emp2_ok, 200, "Member accessing project")
    ok(f"Employee 2 now has access; project team size = {len(r_emp2_ok.json()['members'])}")

    step("Manager updates project metadata...")
    r_upd_proj = client.request(
        "PUT",
        f"/api/v1/projects/{project_id}",
        token=client.token("manager"),
        json_body={
            "name": f"Q4 Zero-Trust Rollout - Live ({run_tag})",
            "description": "Updated during live feature walkthrough.",
            "status": "Active",
            "startDate": today.isoformat(),
            "targetDate": (today + timedelta(days=45)).isoformat(),
        },
    )
    assert_status(r_upd_proj, 200, "Update project")
    ok("Project metadata and target date updated")

    pause_if_requested(args.pause, "Scene 3 (Kanban Tasks, State Machine & Optimistic Concurrency)")

    # =========================================================================
    # SCENE 3: Kanban Tasks, State Machine Guardrails & Optimistic Concurrency
    # =========================================================================
    scene(3, "Kanban Tasks, State Machine & Optimistic Concurrency",
          "Walks a task through Backlog -> ToDo -> InProgress -> Blocked -> InProgress -> Done + checks 400 & 409.")

    step("Manager creates a Backlog task assigned to Employee 1...")
    r_task = client.request(
        "POST",
        f"/api/v1/projects/{project_id}/tasks",
        token=client.token("manager"),
        json_body={
            "title": f"Configure SSO SAML IdP Metadata ({run_tag})",
            "description": "Configure enterprise identity provider and verify JWT claim mapping.",
            "priority": "High",
            "assigneeId": client.user_id("employee"),
            "dueAtUtc": (datetime.now(timezone.utc) + timedelta(days=3)).isoformat(),
            "estimateMinutes": 180,
            "status": "Backlog",
        },
    )
    assert_status(r_task, 201, "Create task")
    task = r_task.json()
    task_id = task["id"]
    ver = task["version"]
    ok(f"Created Task '{task['title']}' in status={task['status']}, version={ver}")

    step("Guardrail test: Invalid state jump Backlog -> Done (expect 400 Bad Request)...")
    r_bad_trans = client.request(
        "PATCH",
        f"/api/v1/tasks/{task_id}/status",
        token=client.token("employee"),
        json_body={"status": "Done", "version": ver, "comment": "Trying to skip workflow"},
    )
    assert_status(r_bad_trans, 400, "Invalid task transition Backlog -> Done")
    ok("Domain state machine rejected Backlog -> Done with HTTP 400")

    transitions = [
        ("ToDo", "Prioritized for current sprint."),
        ("InProgress", "Employee 1 picked up implementation."),
        ("Blocked", "Waiting for IdP certificate from Security team."),
        ("InProgress", "Certificate received; resuming work."),
        ("Done", "SAML integration verified and deployed."),
    ]
    for next_status, comment in transitions:
        r_tr = client.request(
            "PATCH",
            f"/api/v1/tasks/{task_id}/status",
            token=client.token("employee"),
            json_body={"status": next_status, "version": ver, "comment": comment},
        )
        assert_status(r_tr, 200, f"Transition task to {next_status}")
        updated = r_tr.json()
        ver = updated["version"]
        ok(f"Transitioned -> {updated['status']} (new version={ver})")

    step(f"Optimistic concurrency test: Re-submitting stale version={ver - 1} (expect 409 Conflict)...")
    r_stale = client.request(
        "PATCH",
        f"/api/v1/tasks/{task_id}/status",
        token=client.token("manager"),
        json_body={"status": "ToDo", "version": ver - 1, "comment": "Stale client update"},
    )
    assert_status(r_stale, 409, "Stale version concurrency conflict")
    ok("Optimistic concurrency lock returned HTTP 409 Conflict for stale version")

    step("Employee 2 posts a discussion comment & Manager inspects audit history...")
    r_cmt = client.request(
        "POST",
        f"/api/v1/tasks/{task_id}/comments",
        token=client.token("employee2"),
        json_body={"body": "UI login button verified against new SSO flow!"},
    )
    assert_status(r_cmt, 201, "Add task comment")

    r_hist = client.request("GET", f"/api/v1/tasks/{task_id}/history", token=client.token("manager"))
    assert_status(r_hist, 200, "Get task history")
    ok(f"Comment added; task status history contains {len(r_hist.json())} recorded transitions")

    pause_if_requested(args.pause, "Scene 4 (Service Desk & Internal Notes Privacy)")

    # =========================================================================
    # SCENE 4: Service Desk & Strict Internal Notes Privacy Boundary
    # =========================================================================
    scene(4, "Service Desk & Strict Internal Notes Privacy Boundary",
          "Submits ticket, assigns agent, enforces internal note privacy at DB projection layer, and closes ticket.")

    step("Employee 1 submits a High-priority Technical support ticket linked to the project...")
    r_tkt = client.request(
        "POST",
        "/api/v1/tickets",
        token=client.token("employee"),
        json_body={
            "subject": f"Intermittent 502 on SSO callback ({run_tag})",
            "description": "Seeing occasional 502 Bad Gateway after IdP redirect.",
            "category": "Technical",
            "priority": "High",
            "relatedProjectId": project_id,
        },
    )
    assert_status(r_tkt, 201, "Create ticket")
    ticket = r_tkt.json()
    ticket_id = ticket["id"]
    ticket_num = ticket["ticketNumber"]
    ok(f"Created Ticket #{ticket_num} (status={ticket['status']})")

    step("Support Agent lists eligible agents and assigns ticket to self...")
    r_agents = client.request("GET", "/api/v1/tickets/agents", token=client.token("agent"))
    assert_status(r_agents, 200, "List support agents")
    r_assign = client.request(
        "PUT",
        f"/api/v1/tickets/{ticket_id}/assign",
        token=client.token("agent"),
        json_body={"assigneeId": client.user_id("agent"), "comment": "Picking up from triage queue."},
    )
    assert_status(r_assign, 200, "Assign ticket")
    ok(f"Ticket #{ticket_num} assigned to Support Agent (status={r_assign.json()['status']})")

    step("Support Agent moves ticket to InProgress, posts PUBLIC reply + PRIVATE internal note...")
    r_t_prog = client.request(
        "PATCH",
        f"/api/v1/tickets/{ticket_id}/status",
        token=client.token("agent"),
        json_body={"status": "InProgress", "comment": "Investigating reverse proxy buffer size."},
    )
    assert_status(r_t_prog, 200, "Ticket -> InProgress")

    r_pub = client.request(
        "POST",
        f"/api/v1/tickets/{ticket_id}/messages",
        token=client.token("agent"),
        json_body={"body": "We are inspecting the gateway logs—could you share your browser trace?", "isInternalNote": False},
    )
    assert_status(r_pub, 201, "Agent public reply")

    r_priv = client.request(
        "POST",
        f"/api/v1/tickets/{ticket_id}/messages",
        token=client.token("agent"),
        json_body={"body": "INTERNAL ONLY: Looks like large SAML header overflowing nginx proxy_buffer_size.", "isInternalNote": True},
    )
    assert_status(r_priv, 201, "Agent internal note")
    ok("Posted 1 public reply and 1 internal staff note")

    step("Security test: Requester (Employee 1) attempts to post internal note (expect 403)...")
    r_emp_priv = client.request(
        "POST",
        f"/api/v1/tickets/{ticket_id}/messages",
        token=client.token("employee"),
        json_body={"body": "Trying to post internal note", "isInternalNote": True},
    )
    assert_status(r_emp_priv, 403, "Employee posting internal note")
    ok("Requester internal note attempt rejected with HTTP 403 Forbidden")

    step("Privacy boundary check: Compare messages visible to Agent vs Requester...")
    msgs_agent = client.request("GET", f"/api/v1/tickets/{ticket_id}/messages", token=client.token("agent")).json()
    msgs_emp = client.request("GET", f"/api/v1/tickets/{ticket_id}/messages", token=client.token("employee")).json()
    if len(msgs_agent) != 2 or len(msgs_emp) != 1 or any(m["isInternalNote"] for m in msgs_emp):
        raise AssertionError(f"Internal note privacy leaked! agent={len(msgs_agent)}, emp={len(msgs_emp)}")
    ok(f"Privacy verified: Agent sees {len(msgs_agent)} messages (incl. internal), Requester sees {len(msgs_emp)} public message only")

    step("Completing ticket flow: WaitingForRequester -> InProgress -> Resolved -> Closed...")
    for st, note in [
        ("WaitingForRequester", "Requested HAR file."),
        ("InProgress", "Customer provided HAR; increasing proxy buffer."),
        ("Resolved", "Nginx proxy_buffer_size increased; issue resolved."),
        ("Closed", "Customer confirmed fix."),
    ]:
        r_st = client.request(
            "PATCH",
            f"/api/v1/tickets/{ticket_id}/status",
            token=client.token("agent"),
            json_body={"status": st, "comment": note},
        )
        assert_status(r_st, 200, f"Ticket -> {st}")
    ok(f"Ticket #{ticket_num} completed full lifecycle and reached Closed")

    pause_if_requested(args.pause, "Scene 5 (Sequential Multi-Step Approval Engine)")

    # =========================================================================
    # SCENE 5: Sequential Multi-Step Approval Engine (Happy, Rejection & Cancel)
    # =========================================================================
    scene(5, "Sequential Multi-Step Approval Engine",
          "Creates workflow definition, tests 2-step approval happy path, guardrails (403/409), terminal rejection & cancel.")

    step("Manager defines a custom 2-step Workflow Definition...")
    r_wf = client.request(
        "POST",
        "/api/v1/workflow-definitions",
        token=client.token("manager"),
        json_body={
            "name": f"Cloud Infrastructure Spend ({run_tag})",
            "description": "Two-stage review for cloud infra changes",
            "steps": [
                {"stepOrder": 1, "name": "Engineering Lead Review", "approverRole": "Manager", "approverUserId": None},
                {"stepOrder": 2, "name": "Executive Signoff", "approverRole": None, "approverUserId": client.user_id("admin")},
            ],
        },
    )
    assert_status(r_wf, 201, "Create workflow definition")
    wf_id = r_wf.json()["id"]
    ok(f"Created Workflow Definition '{r_wf.json()['name']}' with {len(r_wf.json()['steps'])} steps")

    # 5A: Happy Path (Approve Step 1 -> Approve Step 2 -> Approved)
    step("5A. Happy Path: Employee 1 submits approval request #1...")
    r_app1 = client.request(
        "POST",
        "/api/v1/approvals",
        token=client.token("employee"),
        json_body={
            "workflowDefinitionId": wf_id,
            "title": f"Provision HA Redis Cluster ({run_tag})",
            "description": "Required for distributed rate limiting across API replicas.",
        },
    )
    assert_status(r_app1, 201, "Submit approval request #1")
    app1 = r_app1.json()
    app1_id = app1["id"]
    app1_ver = app1["version"]
    ok(f"Submitted Approval #{app1['requestNumber']} -> Step {app1['currentStepOrder']}: {app1['currentStepName']} (approver={app1['currentApproverName']})")

    step("Guardrail test: Admin (Step 2 approver) tries to approve Step 1 early (expect 403 Forbidden)...")
    r_early = client.request(
        "POST",
        f"/api/v1/approvals/{app1_id}/decisions",
        token=client.token("admin"),
        json_body={"decision": "Approved", "comment": "Out of turn", "expectedVersion": app1_ver},
    )
    assert_status(r_early, 403, "Out-of-turn approver")
    ok("Out-of-turn approval rejected with HTTP 403 Forbidden")

    step("Manager checks inbox (?inboxOnly=true) and approves Step 1...")
    r_inbox = client.request("GET", "/api/v1/approvals", token=client.token("manager"), query={"inboxOnly": "true"})
    assert_status(r_inbox, 200, "Manager approval inbox")
    assert any(x["id"] == app1_id for x in r_inbox.json()["items"]), "Approval missing from Manager inbox"

    r_dec1 = client.request(
        "POST",
        f"/api/v1/approvals/{app1_id}/decisions",
        token=client.token("manager"),
        json_body={"decision": "Approved", "comment": "Architecture reviewed and approved.", "expectedVersion": app1_ver},
    )
    assert_status(r_dec1, 200, "Step 1 approval")
    app1_detail = r_dec1.json()
    app1_ver = app1_detail["version"]
    ok(f"Step 1 Approved -> advanced to Step {app1_detail['currentStepOrder']}: {app1_detail['currentStepName']}")

    step("Double-decision guardrail: Manager tries to vote again with stale version (expect 403/409)...")
    r_dup = client.request(
        "POST",
        f"/api/v1/approvals/{app1_id}/decisions",
        token=client.token("manager"),
        json_body={"decision": "Approved", "comment": "Duplicate click", "expectedVersion": app1_ver - 1},
    )
    assert_status(r_dup, (403, 409), "Double decision / stale version")
    ok(f"Duplicate/stale decision blocked with HTTP {r_dup.status}")

    step("Admin approves Step 2 (Executive Signoff) -> Final Approved state...")
    r_dec2 = client.request(
        "POST",
        f"/api/v1/approvals/{app1_id}/decisions",
        token=client.token("admin"),
        json_body={"decision": "Approved", "comment": "Budget cleared.", "expectedVersion": app1_ver},
    )
    assert_status(r_dec2, 200, "Step 2 approval")
    assert r_dec2.json()["status"] == "Approved"
    ok(f"Approval #{app1['requestNumber']} finalized as status={r_dec2.json()['status']}")

    # 5B: Terminal Rejection Flow
    step("5B. Terminal Rejection Flow: Employee 2 submits request & Manager rejects Step 1...")
    r_app2 = client.request(
        "POST",
        "/api/v1/approvals",
        token=client.token("employee2"),
        json_body={
            "workflowDefinitionId": wf_id,
            "title": f"Unbudgeted GPU Cluster ({run_tag})",
            "description": "Experimental cluster outside quarterly budget.",
        },
    )
    assert_status(r_app2, 201, "Submit approval request #2")
    app2 = r_app2.json()
    r_rej = client.request(
        "POST",
        f"/api/v1/approvals/{app2['id']}/decisions",
        token=client.token("manager"),
        json_body={"decision": "Rejected", "comment": "Defer to next quarter budget cycle.", "expectedVersion": app2["version"]},
    )
    assert_status(r_rej, 200, "Reject approval request #2")
    rej_detail = r_rej.json()
    step2_status = next(s["status"] for s in rej_detail["steps"] if s["stepOrder"] == 2)
    assert rej_detail["status"] == "Rejected" and step2_status == "Skipped"
    ok(f"Terminal rejection verified: Request=#{rej_detail['requestNumber']} is Rejected, Step 2 is {step2_status}")

    # 5C: Requester Cancellation Flow
    step("5C. Cancellation Flow: Employee 1 submits request #3 and cancels it...")
    r_app3 = client.request(
        "POST",
        "/api/v1/approvals",
        token=client.token("employee"),
        json_body={
            "workflowDefinitionId": wf_id,
            "title": f"Duplicate License Request ({run_tag})",
            "description": "Submitted by mistake; cancelling.",
        },
    )
    assert_status(r_app3, 201, "Submit approval request #3")
    app3 = r_app3.json()
    r_cancel = client.request(
        "POST",
        f"/api/v1/approvals/{app3['id']}/cancel",
        token=client.token("employee"),
        json_body={"expectedVersion": app3["version"]},
    )
    assert_status(r_cancel, 200, "Cancel approval request #3")
    assert r_cancel.json()["status"] == "Cancelled"
    ok(f"Cancellation verified: Request #{app3['requestNumber']} is Cancelled")

    pause_if_requested(args.pause, "Scene 6 (Notifications & User Isolation)")

    # =========================================================================
    # SCENE 6: Real-Time In-App Notifications & User Isolation
    # =========================================================================
    scene(6, "Real-Time In-App Notifications & User Isolation",
          "Checks unread notification counts, isolation (404 on other user's notification), and read receipts.")

    r_emp_notifs = client.request("GET", "/api/v1/notifications", token=client.token("employee"), query={"unreadOnly": "true"})
    assert_status(r_emp_notifs, 200, "List employee notifications")
    emp_notifs = r_emp_notifs.json()
    ok(f"Employee 1 has {emp_notifs['unreadCount']} unread notification(s)")

    if emp_notifs["items"]:
        target_notif_id = emp_notifs["items"][0]["id"]
        step("Security check: Employee 2 attempts to mark Employee 1's notification read (expect 404)...")
        r_iso = client.request("PATCH", f"/api/v1/notifications/{target_notif_id}/read", token=client.token("employee2"))
        assert_status(r_iso, 404, "Notification user isolation")
        ok("Cross-user notification modification returned HTTP 404 Not Found")

        r_read_one = client.request("PATCH", f"/api/v1/notifications/{target_notif_id}/read", token=client.token("employee"))
        assert_status(r_read_one, 204, "Mark own notification read")
        ok("Employee 1 marked notification as read (HTTP 204)")

    r_read_all = client.request("POST", "/api/v1/notifications/read-all", token=client.token("manager"))
    assert_status(r_read_all, 204, "Manager mark all read")
    r_cnt = client.request("GET", "/api/v1/notifications/unread-count", token=client.token("manager"))
    assert_status(r_cnt, 200, "Manager unread count")
    ok(f"Manager marked all read -> unread count is now {r_cnt.json()['count']}")

    pause_if_requested(args.pause, "Scene 7 (Executive Dashboard & RFC 4180 CSV Reports)")

    # =========================================================================
    # SCENE 7: Executive Dashboard KPIs & RFC 4180 CSV Reports
    # =========================================================================
    scene(7, "Executive Dashboard KPIs & RFC 4180 CSV Reports",
          "Fetches role-tailored dashboard summaries, task/ticket/project analytics, and CSV exports.")

    for alias in ("employee", "manager"):
        r_dash = client.request("GET", "/api/v1/dashboard/summary", token=client.token(alias))
        assert_status(r_dash, 200, f"Dashboard summary ({alias})")
        d = r_dash.json()
        ok(
            f"Dashboard ({alias}): assignedTasks={d['assignedTasksCount']}, "
            f"openTickets={d['openTicketsCount']}, pendingApprovals={d['pendingApprovalsCount']}, "
            f"managedProjects={d['managedProjectsCount']}"
        )

    r_task_rep = client.request("GET", "/api/v1/reports/tasks", token=client.token("manager"), query={"projectId": project_id})
    assert_status(r_task_rep, 200, "Task report")
    tr = r_task_rep.json()
    ok(f"Task Report ({proj_code}): total={tr['totalTasks']}, completed={tr['completedTasks']}, completionRate={tr['completionRate']}%")

    r_proj_perf = client.request("GET", f"/api/v1/reports/projects/{project_id}", token=client.token("manager"))
    assert_status(r_proj_perf, 200, "Project performance report")
    ok(f"Project Performance ({proj_code}): memberCount={r_proj_perf.json()['memberCount']}")

    r_tkt_rep = client.request("GET", "/api/v1/reports/tickets", token=client.token("manager"))
    assert_status(r_tkt_rep, 200, "Ticket report")
    tktr = r_tkt_rep.json()
    ok(f"Ticket Report: total={tktr['totalTickets']}, resolved={tktr['resolvedTickets']}, resolutionRate={tktr['resolutionRate']}%")

    r_csv_tasks = client.request("GET", "/api/v1/reports/tasks", token=client.token("manager"), query={"exportCsv": "true"})
    assert_status(r_csv_tasks, 200, "Export tasks CSV")
    r_csv_tickets = client.request("GET", "/api/v1/reports/tickets", token=client.token("manager"), query={"exportCsv": "true"})
    assert_status(r_csv_tickets, 200, "Export tickets CSV")
    ok(f"CSV Exports ready (tasks={len(r_csv_tasks.body_bytes)} bytes, tickets={len(r_csv_tickets.body_bytes)} bytes)")

    if args.save_csv_dir:
        os.makedirs(args.save_csv_dir, exist_ok=True)
        with open(os.path.join(args.save_csv_dir, f"tasks-{run_tag}.csv"), "wb") as f:
            f.write(r_csv_tasks.body_bytes)
        with open(os.path.join(args.save_csv_dir, f"tickets-{run_tag}.csv"), "wb") as f:
            f.write(r_csv_tickets.body_bytes)
        info(f"Saved CSV exports to {os.path.abspath(args.save_csv_dir)}")

    pause_if_requested(args.pause, "Scene 8 (Admin Governance, Session Revocation & Audit Logs)")

    # =========================================================================
    # SCENE 8: Administration, User Lifecycle, Sole-Admin Guard & Audit Logs
    # =========================================================================
    scene(8, "Administration, Session Revocation, Sole-Admin Guard & Audit Logs",
          "Provisions a user, promotes roles, verifies deactivation revokes sessions, and inspects audit trail.")

    step("Negative RBAC test: Manager tries to access /api/v1/admin/users (expect 403)...")
    r_non_admin = client.request("GET", "/api/v1/admin/users", token=client.token("manager"))
    assert_status(r_non_admin, 403, "Manager accessing admin endpoint")
    ok("Non-admin access to /api/v1/admin/users blocked with HTTP 403 Forbidden")

    new_email = f"contractor.{run_tag}@workflowpro.local"
    new_pwd = "DemoPass@123"
    step(f"Admin provisions new user {new_email} with Employee role...")
    r_create_u = client.request(
        "POST",
        "/api/v1/admin/users",
        token=client.token("admin"),
        json_body={
            "email": new_email,
            "displayName": f"Jordan Contractor ({run_tag})",
            "password": new_pwd,
            "roles": ["Employee"],
        },
    )
    assert_status(r_create_u, 201, "Admin create user")
    new_user_id = r_create_u.json()["id"]
    ok(f"Provisioned user {new_email} (id={new_user_id})")

    step("Admin promotes new user to SupportAgent...")
    r_roles = client.request(
        "PUT",
        f"/api/v1/admin/users/{new_user_id}/roles",
        token=client.token("admin"),
        json_body={"roles": ["Employee", "SupportAgent"]},
    )
    assert_status(r_roles, 200, "Update user roles")
    ok(f"Updated roles -> {r_roles.json()['roles']}")

    step("Contractor logs in, then Admin deactivates contractor to verify session revocation...")
    contractor_client = ApiClient(base_url)
    contractor_client.login("contractor", new_email, new_pwd)

    r_deact = client.request(
        "PATCH",
        f"/api/v1/admin/users/{new_user_id}/status",
        token=client.token("admin"),
        json_body={"isActive": False},
    )
    assert_status(r_deact, 200, "Deactivate contractor")
    ok("Admin deactivated contractor user")

    r_revoked = contractor_client.request("POST", "/api/v1/auth/refresh", json_body={})
    assert_status(r_revoked, 401, "Revoked refresh token check")
    ok("Contractor's refresh token was immediately revoked (HTTP 401 Unauthorized)")

    step("Sole-Admin Protection check: Admin attempts to deactivate own admin account (expect 400/409)...")
    r_self_deact = client.request(
        "PATCH",
        f"/api/v1/admin/users/{client.user_id('admin')}/status",
        token=client.token("admin"),
        json_body={"isActive": False},
    )
    assert_status(r_self_deact, (400, 409), "Self/sole admin deactivation protection")
    ok(f"Sole/self admin deactivation blocked with HTTP {r_self_deact.status}")

    step("Admin queries immutable Audit Log Explorer...")
    r_audit = client.request("GET", "/api/v1/admin/audit-logs", token=client.token("admin"), query={"page": 1, "pageSize": 10})
    assert_status(r_audit, 200, "Query audit logs")
    logs = r_audit.json()["items"]
    ok(f"Audit Log Explorer returned {r_audit.json()['totalCount']} total entries; latest={logs[0]['action']} on {logs[0]['entityType']}")

    step("Archive demo project to leave workspace tidy...")
    r_arch = client.request("POST", f"/api/v1/projects/{project_id}/archive", token=client.token("manager"))
    assert_status(r_arch, 204, "Archive demo project")
    ok(f"Archived demo project {proj_code} (HTTP 204)")

    banner("ALL 8 WORKFLOWPRO MODULES & END-TO-END FLOWS VERIFIED SUCCESSFULLY!")
    print(f"  Summary of created demo artifacts (runTag={run_tag}):")
    print(f"    • Project : {proj_code} ({project_id}) [Archived]")
    print(f"    • Task    : {task_id} [Backlog -> ToDo -> InProgress -> Blocked -> InProgress -> Done]")
    print(f"    • Ticket  : #{ticket_num} ({ticket_id}) [New -> Assigned -> InProgress -> Waiting -> Resolved -> Closed]")
    print(f"    • Approvals: #{app1['requestNumber']} (Approved), #{app2['requestNumber']} (Rejected/Skipped), #{app3['requestNumber']} (Cancelled)")
    print(f"    • User    : {new_email} [Provisioned, Promoted, Deactivated & Session Revoked]\n")
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run an end-to-end WorkFlowPro demo across all features and security flows."
    )
    parser.add_argument(
        "--base-url",
        help="API base URL (auto-detects http://localhost:5225, :8080, or :80 if omitted)",
    )
    parser.add_argument(
        "--pause",
        action="store_true",
        help="Pause between scenes for live narrated walkthroughs",
    )
    parser.add_argument(
        "--save-csv-dir",
        help="Optional directory to write exported task/ticket CSV reports",
    )
    parser.add_argument("--admin-email", default="admin@workflowpro.local")
    parser.add_argument("--admin-password", default="Admin@123456")
    parser.add_argument("--demo-password", default="Password@123")
    return parser.parse_args()


if __name__ == "__main__":
    try:
        sys.exit(run_demo(parse_args()))
    except Exception as exc:
        fail(str(exc))
        sys.exit(1)
