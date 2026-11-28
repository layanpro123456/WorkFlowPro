using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using WorkFlowPro.Api.Middleware;
using WorkFlowPro.Api.Services;
using WorkFlowPro.Application;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Infrastructure;
using WorkFlowPro.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Configure Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthPolicy", httpContext =>
    {
        var clientIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString();

        var key = clientIp ?? ("client-" + httpContext.TraceIdentifier);

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 15,
            QueueLimit = 0
        });
    });

    options.AddPolicy("WebhookPolicy", httpContext =>
    {
        var clientIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString();

        var key = clientIp ?? ("webhook-" + httpContext.TraceIdentifier);

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 120,
            QueueLimit = 0
        });
    });
});

// Configure CORS
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Configure JWT Authentication
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "WorkFlowPro";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "WorkFlowPro.Web";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Local development; handled by reverse proxy in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

// Configure Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.Administrator, policy => policy.RequireRole(Roles.Administrator));
    options.AddPolicy(Roles.Manager, policy => policy.RequireRole(Roles.Administrator, Roles.Manager));
    options.AddPolicy(Roles.SupportAgent, policy => policy.RequireRole(Roles.Administrator, Roles.SupportAgent));
    options.AddPolicy(Roles.Employee, policy => policy.RequireRole(Roles.Administrator, Roles.Manager, Roles.Employee, Roles.SupportAgent));
});

// OpenAPI setup
builder.Services.AddOpenApi();

var app = builder.Build();

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Security Headers Middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

// Database initialisation & synthetic seeding in Development / container startup
var applyMigrations = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ApplyMigrationsAtStartup");
if (applyMigrations)
{
    using var scope = app.Services.CreateScope();
    var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
    await initialiser.InitialiseAsync();
    await initialiser.SeedAsync();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "WorkFlowPro API Reference";
            options.Theme = ScalarTheme.Purple;
        });
    }
}

app.UseHttpsRedirection();

app.UseCors("DefaultCorsPolicy");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints for probes and monitoring
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapControllers();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
