namespace WorkFlowPro.Domain.Constants;

public static class AuditActions
{
    public const string UserCreated = "UserCreated";
    public const string UserActivated = "UserActivated";
    public const string UserDeactivated = "UserDeactivated";
    public const string UserRolesUpdated = "UserRolesUpdated";

    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";

    public const string ProjectCreated = "ProjectCreated";
    public const string ProjectArchived = "ProjectArchived";
    public const string ProjectMemberAdded = "ProjectMemberAdded";
    public const string ProjectMemberRemoved = "ProjectMemberRemoved";

    public const string TaskCreated = "TaskCreated";
    public const string TaskStatusChanged = "TaskStatusChanged";

    public const string TicketCreated = "TicketCreated";
    public const string TicketStatusChanged = "TicketStatusChanged";

    public const string ApprovalSubmitted = "ApprovalSubmitted";
    public const string ApprovalDecided = "ApprovalDecided";
    public const string ApprovalCancelled = "ApprovalCancelled";

    public const string GitRepositoryConnected = "GitRepositoryConnected";
    public const string GitRepositoryUpdated = "GitRepositoryUpdated";
    public const string GitRepositoryDisconnected = "GitRepositoryDisconnected";
    public const string GitBranchCreated = "GitBranchCreated";
    public const string GitPullRequestLinked = "GitPullRequestLinked";
    public const string GitPullRequestUnlinked = "GitPullRequestUnlinked";
    public const string GitWebhookProcessed = "GitWebhookProcessed";
}
