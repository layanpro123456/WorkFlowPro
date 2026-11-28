using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkFlowPro.Application.Features.Git.Dtos;
using WorkFlowPro.Application.Features.Git.Services;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("WebhookPolicy")]
[Route("api/v1/webhooks/git")]
public class WebhooksController : ControllerBase
{
    private const int MaxPayloadBytes = 2 * 1024 * 1024; // 2 MB guard
    private readonly IGitIntegrationAppService _gitService;

    public WebhooksController(IGitIntegrationAppService gitService)
    {
        _gitService = gitService;
    }

    [HttpPost("{provider}/{repositoryId:guid}")]
    [RequestSizeLimit(MaxPayloadBytes)]
    [ProducesResponseType(typeof(GitWebhookProcessResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GitWebhookProcessResultDto>> ReceiveGitWebhook(
        [FromRoute] string provider,
        [FromRoute] Guid repositoryId,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);

        var deliveryId = Request.Headers["X-GitHub-Delivery"].FirstOrDefault();
        var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault();
        var signatureHeader = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

        var result = await _gitService.ProcessWebhookAsync(
            provider: provider,
            repositoryId: repositoryId,
            deliveryId: deliveryId,
            eventType: eventType,
            signatureHeader: signatureHeader,
            rawPayload: rawPayload,
            cancellationToken: cancellationToken);

        return Ok(result);
    }
}
