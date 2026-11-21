namespace WorkFlowPro.Application.Features.Projects.Dtos;

public record AddProjectMemberRequest(
    Guid UserId,
    string ProjectRole
);
