using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Approvals.Dtos;

namespace WorkFlowPro.Application.Features.Approvals.Services;

public interface IApprovalAppService
{
    // Workflow Definitions
    Task<IReadOnlyList<WorkflowDefinitionSummaryDto>> ListDefinitionsAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<WorkflowDefinitionDetailDto> GetDefinitionByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<WorkflowDefinitionDetailDto> CreateDefinitionAsync(CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken);
    Task<WorkflowDefinitionDetailDto> UpdateDefinitionAsync(Guid id, UpdateWorkflowDefinitionRequest request, CancellationToken cancellationToken);

    // Approval Requests
    Task<PagedResult<ApprovalRequestSummaryDto>> ListRequestsAsync(
        string? status,
        bool? inboxOnly,
        bool? myRequestsOnly,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ApprovalRequestDetailDto> GetRequestByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ApprovalRequestSummaryDto> SubmitRequestAsync(SubmitApprovalRequest request, CancellationToken cancellationToken);
    Task<ApprovalRequestDetailDto> RecordDecisionAsync(Guid id, RecordDecisionRequest request, CancellationToken cancellationToken);
    Task<ApprovalRequestDetailDto> CancelRequestAsync(Guid id, CancelApprovalRequest request, CancellationToken cancellationToken);
}
