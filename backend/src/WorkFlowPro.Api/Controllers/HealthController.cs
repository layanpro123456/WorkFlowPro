using Microsoft.AspNetCore.Mvc;
using WorkFlowPro.Application.Common.Models;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthCheckResponse> Get()
    {
        return Ok(new HealthCheckResponse(
            Status: "Healthy",
            TimestampUtc: DateTime.UtcNow,
            Version: "1.0.0"
        ));
    }
}
