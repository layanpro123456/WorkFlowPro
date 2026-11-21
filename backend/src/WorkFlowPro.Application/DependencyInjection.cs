using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Features.Auth.Services;
using WorkFlowPro.Application.Features.Projects.Services;

namespace WorkFlowPro.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAuthAppService, AuthAppService>();
        services.AddScoped<IProjectAppService, ProjectAppService>();

        return services;
    }
}
