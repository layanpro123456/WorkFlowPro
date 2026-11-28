export type GitProvider = 'GitHub';
export type PullRequestState = 'Open' | 'Draft' | 'Merged' | 'Closed';
export type PullRequestReviewStatus = 'Pending' | 'Approved' | 'ChangesRequested' | 'Commented';
export type PullRequestCheckStatus = 'None' | 'Pending' | 'Success' | 'Failure';

export interface ProjectGitRepository {
  id: string;
  projectId: string;
  provider: GitProvider;
  repositoryOwner: string;
  repositoryName: string;
  repositoryFullName: string;
  apiBaseUrl: string;
  webBaseUrl: string;
  defaultBranch: string;
  hasAccessToken: boolean;
  hasWebhookSecret: boolean;
  webhookPath: string;
  isDefault: boolean;
  isActive: boolean;
  autoLinkEnabled: boolean;
  autoTransitionEnabled: boolean;
  useInReviewColumn: boolean;
  lastSyncedAtUtc?: string | null;
  lastSyncError?: string | null;
  createdAtUtc: string;
}

export interface ConnectGitRepositoryRequest {
  provider: GitProvider;
  repositoryOwner: string;
  repositoryName: string;
  defaultBranch?: string | null;
  apiBaseUrl?: string | null;
  webBaseUrl?: string | null;
  accessToken?: string | null;
  webhookSecret?: string | null;
  isDefault: boolean;
  autoLinkEnabled: boolean;
  autoTransitionEnabled: boolean;
  useInReviewColumn: boolean;
}

export interface UpdateGitRepositoryRequest {
  defaultBranch: string;
  apiBaseUrl?: string | null;
  webBaseUrl?: string | null;
  accessToken?: string | null;
  webhookSecret?: string | null;
  isDefault: boolean;
  isActive: boolean;
  autoLinkEnabled: boolean;
  autoTransitionEnabled: boolean;
  useInReviewColumn: boolean;
}

export interface ConnectGitRepositoryResponse {
  repository: ProjectGitRepository;
  generatedWebhookSecret?: string | null;
}

export interface GitConnectionTestResult {
  success: boolean;
  message: string;
  detectedDefaultBranch?: string | null;
  isPrivate: boolean;
}

export interface GitSyncResult {
  repositoryId: string;
  pullRequestsSynced: number;
  tasksLinked: number;
  tasksTransitioned: number;
  syncedAtUtc: string;
}

export interface TaskGitBranch {
  id: string;
  taskId: string;
  repositoryId: string;
  repositoryFullName: string;
  branchName: string;
  baseBranch: string;
  branchUrl: string;
  createdByUserId?: string | null;
  createdAtUtc: string;
}

export interface CreateTaskBranchRequest {
  repositoryId?: string | null;
  branchName?: string | null;
  baseBranch?: string | null;
  branchPrefix?: string | null;
}

export interface TaskPullRequest {
  id: string;
  repositoryId: string;
  repositoryFullName: string;
  prNumber: number;
  title: string;
  state: PullRequestState;
  sourceBranch: string;
  targetBranch: string;
  authorLogin: string;
  authorAvatarUrl?: string | null;
  htmlUrl: string;
  reviewStatus: PullRequestReviewStatus;
  checkStatus: PullRequestCheckStatus;
  commitsCount: number;
  additions: number;
  deletions: number;
  linkSource: string;
  openedAtUtc: string;
  mergedAtUtc?: string | null;
  closedAtUtc?: string | null;
  lastSyncedAtUtc: string;
}

export interface ProjectPullRequestSummary {
  id: string;
  repositoryId: string;
  repositoryFullName: string;
  prNumber: number;
  title: string;
  state: PullRequestState;
  sourceBranch: string;
  targetBranch: string;
  authorLogin: string;
  htmlUrl: string;
  reviewStatus: PullRequestReviewStatus;
  checkStatus: PullRequestCheckStatus;
  linkedTaskKeys: string[];
  openedAtUtc: string;
  mergedAtUtc?: string | null;
  lastSyncedAtUtc: string;
}

export interface LinkPullRequestRequest {
  repositoryId: string;
  prNumber: number;
}

export interface TaskGitDetail {
  taskId: string;
  taskKey: string;
  branches: TaskGitBranch[];
  pullRequests: TaskPullRequest[];
}

export interface TaskGitSummary {
  branchCount: number;
  openPrCount: number;
  mergedPrCount: number;
  primaryPrNumber?: number | null;
  primaryPrState?: PullRequestState | null;
  primaryPrReviewStatus?: PullRequestReviewStatus | null;
  primaryPrCheckStatus?: PullRequestCheckStatus | null;
  primaryPrUrl?: string | null;
  primaryBranchName?: string | null;
  primaryBranchUrl?: string | null;
}
