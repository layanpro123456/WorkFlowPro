using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Application.Features.Auth.Services;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("AuthPolicy")]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "workflowpro_refresh_token";
    private readonly IAuthAppService _authAppService;

    public AuthController(IAuthAppService authAppService)
    {
        _authAppService = authAppService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var deviceLabel = Request.Headers.UserAgent.ToString();
        var (response, rawRefreshToken) = await _authAppService.LoginAsync(request, deviceLabel, cancellationToken);

        SetRefreshTokenCookie(rawRefreshToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Refresh(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName] ?? request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "No refresh token provided."
            });
        }

        var deviceLabel = Request.Headers.UserAgent.ToString();
        var (response, newRawRefreshToken) = await _authAppService.RefreshTokenAsync(refreshToken, deviceLabel, cancellationToken);

        SetRefreshTokenCookie(newRawRefreshToken);
        return Ok(response);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName] ?? request?.RefreshToken;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _authAppService.LogoutAsync(refreshToken, cancellationToken);
        }

        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var profile = await _authAppService.GetCurrentUserAsync(cancellationToken);
        return Ok(profile);
    }

    private void SetRefreshTokenCookie(string rawRefreshToken)
    {
        Response.Cookies.Append(RefreshTokenCookieName, rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/"
        });
    }
}
