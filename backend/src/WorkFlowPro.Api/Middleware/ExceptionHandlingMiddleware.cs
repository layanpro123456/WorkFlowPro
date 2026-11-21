using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;
        _logger.LogError(exception, "Unhandled exception occurred. TraceIdentifier: {CorrelationId}", correlationId);

        context.Response.ContentType = "application/problem+json";

        ProblemDetails problemDetails;

        switch (exception)
        {
            case ValidationException validationException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                problemDetails = new ValidationProblemDetails(
                    validationException.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation Failed",
                    Detail = "One or more validation errors occurred.",
                    Instance = context.Request.Path
                };
                break;

            case UnauthorizedAccessException unauthorizedException:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = unauthorizedException.Message,
                    Instance = context.Request.Path
                };
                break;

            case ForbiddenException forbiddenException:
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = forbiddenException.Message,
                    Instance = context.Request.Path
                };
                break;

            case KeyNotFoundException notFoundException:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = notFoundException.Message,
                    Instance = context.Request.Path
                };
                break;

            case DomainException domainException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Domain Rule Violation",
                    Detail = domainException.Message,
                    Instance = context.Request.Path
                };
                break;

            case InvalidOperationException invalidOpException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid Operation",
                    Detail = invalidOpException.Message,
                    Instance = context.Request.Path
                };
                break;

            case ArgumentException argException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid Argument",
                    Detail = argException.Message,
                    Instance = context.Request.Path
                };
                break;

            case ConcurrencyException concurrencyException:
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Concurrency Conflict",
                    Detail = concurrencyException.Message,
                    Instance = context.Request.Path
                };
                break;

            case DbUpdateConcurrencyException:
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Concurrency Conflict",
                    Detail = "The resource was modified or deleted by another operation. Please refresh and try again.",
                    Instance = context.Request.Path
                };
                break;

            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred. Please reference the correlation ID when contacting support.",
                    Instance = context.Request.Path
                };
                break;
        }

        problemDetails.Extensions["correlationId"] = correlationId;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}
