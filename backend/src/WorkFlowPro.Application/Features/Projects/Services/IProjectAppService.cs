using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Projects.Dtos;

namespace WorkFlowPro.Application.Features.Projects.Services;

public interface IProjectAppService
{
    Task<ProjectSummaryDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);

    Task<PagedResult<ProjectSummaryDto>> GetPagedListAsync(
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ProjectDetailDto?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectDetailDto> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken);

    Task ArchiveAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectMemberDto>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken);

    Task RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);
}
