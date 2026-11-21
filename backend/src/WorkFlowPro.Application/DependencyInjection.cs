using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Features.Admin.Services;
using WorkFlowPro.Application.Features.Approvals.Services;
using WorkFlowPro.Application.Features.Auth.Services;
using WorkFlowPro.Application.Features.Dashboard.Services;
using WorkFlowPro.Application.Features.Notifications.Services;
using WorkFlowPro.Application.Features.Projects.Services;
using WorkFlowPro.Application.Features.Reports.Services;
using WorkFlowPro.Application.Features.Tasks.Services;
using WorkFlowPro.Application.Features.Tickets.Services;

namespace WorkFlowPro.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAuthAppService, AuthAppService>();
        services.AddScoped<IProjectAppService, ProjectAppService>();
        services.AddScoped<ITaskAppService, TaskAppService>();
        services.AddScoped<ITicketAppService, TicketAppService>();
        services.AddScoped<IApprovalAppService, ApprovalAppService>();
        services.AddScoped<INotificationAppService, NotificationAppService>();
        services.AddScoped<IDashboardAppService, DashboardAppService>();
        services.AddScoped<IReportAppService, ReportAppService>();
        services.AddScoped<IAdminAppService, AdminAppService>();

        return services;
    }
}
