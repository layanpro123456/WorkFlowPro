using WorkFlowPro.Application.Features.Dashboard.Dtos;

namespace WorkFlowPro.Application.Features.Dashboard.Services;

public interface IDashboardAppService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}
