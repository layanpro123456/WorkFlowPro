export type ApprovalRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
export type ApprovalStepStatus = 'Pending' | 'Approved' | 'Rejected' | 'Skipped';
export type ApprovalDecisionType = 'Approved' | 'Rejected';

export const APPROVAL_REQUEST_STATUSES: ApprovalRequestStatus[] = [
  'Pending',
  'Approved',
  'Rejected',
  'Cancelled'
];

export interface WorkflowDefinitionSummary {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  stepCount: number;
  createdById: string;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface WorkflowStep {
  id: string;
  stepOrder: number;
  name: string;
  approverRole?: string | null;
  approverUserId?: string | null;
  approverDisplayName?: string | null;
}

export interface WorkflowDefinitionDetail {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  createdById: string;
  createdByName: string;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  steps: WorkflowStep[];
}

export interface WorkflowStepInput {
  stepOrder: number;
  name: string;
  approverRole?: string | null;
  approverUserId?: string | null;
}

export interface CreateWorkflowDefinitionRequest {
  name: string;
  description?: string | null;
  steps: WorkflowStepInput[];
}

export interface UpdateWorkflowDefinitionRequest {
  name: string;
  description?: string | null;
  isActive: boolean;
  steps: WorkflowStepInput[];
}

export interface StepApproverOverride {
  stepOrder: number;
  approverUserId: string;
}

export interface SubmitApprovalRequest {
  workflowDefinitionId: string;
  title: string;
  description: string;
  stepApproverOverrides?: StepApproverOverride[] | null;
}

export interface ApprovalDecision {
  id: string;
  approverId: string;
  approverName: string;
  decision: ApprovalDecisionType;
  comment?: string | null;
  decidedAtUtc: string;
}

export interface ApprovalRequestStep {
  id: string;
  stepOrder: number;
  name: string;
  approverUserId: string;
  approverName: string;
  approverEmail: string;
  status: ApprovalStepStatus;
  decidedAtUtc?: string | null;
  decision?: ApprovalDecision | null;
}

export interface ApprovalRequestSummary {
  id: string;
  requestNumber: number;
  workflowDefinitionId: string;
  workflowName: string;
  requesterId: string;
  requesterName: string;
  title: string;
  status: ApprovalRequestStatus;
  currentStepOrder?: number | null;
  currentStepName?: string | null;
  currentApproverId?: string | null;
  currentApproverName?: string | null;
  version: number;
  submittedAtUtc: string;
  completedAtUtc?: string | null;
}

export interface ApprovalRequestDetail {
  id: string;
  requestNumber: number;
  workflowDefinitionId: string;
  workflowName: string;
  requesterId: string;
  requesterName: string;
  requesterEmail: string;
  title: string;
  description: string;
  status: ApprovalRequestStatus;
  currentStepOrder?: number | null;
  currentStepName?: string | null;
  currentApproverId?: string | null;
  currentApproverName?: string | null;
  version: number;
  submittedAtUtc: string;
  completedAtUtc?: string | null;
  steps: ApprovalRequestStep[];
}

export interface RecordDecisionRequest {
  decision: ApprovalDecisionType;
  comment?: string | null;
  expectedVersion: number;
}

export interface CancelApprovalRequest {
  expectedVersion: number;
}
