using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Native ASP.NET Core 9 OpenAPI setup
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "WorkFlowPro API Reference";
        options.Theme = ScalarTheme.Purple;
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Health check endpoints for probes and monitoring
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapControllers();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
