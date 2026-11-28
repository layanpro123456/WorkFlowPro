using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Infrastructure.Git;
using WorkFlowPro.Infrastructure.Identity;
using WorkFlowPro.Infrastructure.Persistence;
using WorkFlowPro.Infrastructure.Services;

namespace WorkFlowPro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var useSqlite = configuration.GetValue<bool>("UseSqlite") || string.IsNullOrWhiteSpace(connectionString);

        if (useSqlite)
        {
            var sqliteConnection = configuration.GetConnectionString("SqliteConnection") ?? "Data Source=workflowpro.db";
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(sqliteConnection));
        }
        else
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString, b =>
                    b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
        }

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ApplicationDbContextInitialiser>();

        // Configure ASP.NET Core Identity Core
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.AddTransient<IIdentityService, IdentityService>();
        services.AddTransient<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuditService, AuditService>();

        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName("WorkFlowPro");
        var keysDir = configuration["DataProtection:KeysDirectory"];
        if (!string.IsNullOrWhiteSpace(keysDir))
        {
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keysDir));
        }

        services.AddSingleton<IGitSecretProtector, GitSecretProtector>();
        services.AddHttpClient<IGitProviderClient, GitHubProviderClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
