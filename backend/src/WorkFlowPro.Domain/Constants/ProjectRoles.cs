namespace WorkFlowPro.Domain.Constants;

public static class ProjectRoles
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Member = "Member";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Owner,
        Manager,
        Member
    };
}
