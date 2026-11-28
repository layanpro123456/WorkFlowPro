using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Tasks.Dtos;

namespace WorkFlowPro.Application.Features.Tasks.Services;

public interface ITaskAppService
{
    Task<TaskSummaryDto> CreateAsync(
        Guid projectId,
        CreateTaskRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<TaskSummaryDto>> GetProjectTasksAsync(
        Guid projectId,
        string? status,
        string? priority,
        Guid? assigneeId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken,
        string? prState = null);

    Task<TaskDetailDto> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken);

    Task<TaskSummaryDto> UpdateAsync(
        Guid taskId,
        UpdateTaskRequest request,
        CancellationToken cancellationToken);

    Task<TaskSummaryDto> ChangeStatusAsync(
        Guid taskId,
        ChangeTaskStatusRequest request,
        CancellationToken cancellationToken);

    Task<TaskCommentDto> AddCommentAsync(
        Guid taskId,
        AddTaskCommentRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskCommentDto>> GetCommentsAsync(
        Guid taskId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskStatusHistoryDto>> GetHistoryAsync(
        Guid taskId,
        CancellationToken cancellationToken);
}
