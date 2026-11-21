export type TicketStatus =
  | 'New'
  | 'Assigned'
  | 'InProgress'
  | 'WaitingForRequester'
  | 'Resolved'
  | 'Closed';

export type TicketPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export type TicketCategory =
  | 'Technical'
  | 'Billing'
  | 'General'
  | 'Hardware'
  | 'Access'
  | 'BugReport';

export const TICKET_STATUSES: TicketStatus[] = [
  'New',
  'Assigned',
  'InProgress',
  'WaitingForRequester',
  'Resolved',
  'Closed'
];

export const TICKET_PRIORITIES: TicketPriority[] = [
  'Low',
  'Normal',
  'High',
  'Urgent'
];

export const TICKET_CATEGORIES: TicketCategory[] = [
  'Technical',
  'Billing',
  'General',
  'Hardware',
  'Access',
  'BugReport'
];

export const ALLOWED_TICKET_TRANSITIONS: Record<TicketStatus, TicketStatus[]> = {
  New: ['Assigned', 'InProgress', 'Closed'],
  Assigned: ['InProgress', 'WaitingForRequester', 'Resolved', 'Closed'],
  InProgress: ['WaitingForRequester', 'Resolved', 'Closed'],
  WaitingForRequester: ['InProgress', 'Resolved', 'Closed'],
  Resolved: ['Closed', 'InProgress'],
  Closed: ['InProgress']
};

export interface TicketSummary {
  id: string;
  ticketNumber: number;
  subject: string;
  category: TicketCategory;
  priority: TicketPriority;
  status: TicketStatus;
  requesterId: string;
  requesterName: string;
  assigneeId?: string | null;
  assigneeName?: string | null;
  relatedProjectId?: string | null;
  relatedProjectCode?: string | null;
  relatedProjectName?: string | null;
  messageCount: number;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  resolvedAtUtc?: string | null;
  closedAtUtc?: string | null;
}

export interface TicketMessage {
  id: string;
  ticketId: string;
  authorId: string;
  authorName: string;
  body: string;
  isInternalNote: boolean;
  createdAtUtc: string;
}

export interface TicketStatusHistory {
  id: string;
  ticketId: string;
  fromStatus?: TicketStatus | null;
  toStatus: TicketStatus;
  changedById: string;
  changedByName: string;
  changedAtUtc: string;
  comment?: string | null;
}

export interface TicketDetail {
  id: string;
  ticketNumber: number;
  subject: string;
  description: string;
  category: TicketCategory;
  priority: TicketPriority;
  status: TicketStatus;
  requesterId: string;
  requesterName: string;
  requesterEmail: string;
  assigneeId?: string | null;
  assigneeName?: string | null;
  relatedProjectId?: string | null;
  relatedProjectCode?: string | null;
  relatedProjectName?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  resolvedAtUtc?: string | null;
  closedAtUtc?: string | null;
  messages: TicketMessage[];
  history: TicketStatusHistory[];
}

export interface SupportAgent {
  userId: string;
  displayName: string;
  email: string;
  primaryRole: string;
}

export interface CreateTicketRequest {
  subject: string;
  description: string;
  category: TicketCategory;
  priority: TicketPriority;
  relatedProjectId?: string | null;
}

export interface AssignTicketRequest {
  assigneeId: string;
  comment?: string | null;
}

export interface ChangeTicketStatusRequest {
  status: TicketStatus;
  comment?: string | null;
}

export interface AddTicketMessageRequest {
  body: string;
  isInternalNote?: boolean;
}
