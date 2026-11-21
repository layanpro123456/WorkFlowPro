using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Application.Features.Auth.Services;

public class AuthAppService : IAuthAppService
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthAppService(
        IIdentityService identityService,
        IJwtTokenService jwtTokenService,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IValidator<LoginRequest> loginValidator)
    {
        _identityService = identityService;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _currentUserService = currentUserService;
        _loginValidator = loginValidator;
    }

    public async Task<(AuthResponseDto Response, string RawRefreshToken)> LoginAsync(
        LoginRequest request,
        string? deviceLabel,
        CancellationToken cancellationToken)
    {
        var validationResult = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var (success, isInactive, userId, email, displayName, roles) =
            await _identityService.CheckCredentialsAsync(request.Email, request.Password, cancellationToken);

        if (isInactive)
        {
            throw new UnauthorizedAccessException("Account is deactivated. Please contact an administrator.");
        }

        if (!success)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var (accessToken, expiresAtUtc) = _jwtTokenService.GenerateAccessToken(userId, email, displayName, roles);
        var (rawRefreshToken, tokenHash, refreshExpiresAtUtc) = _jwtTokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken(userId, tokenHash, refreshExpiresAtUtc, deviceLabel);
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var responseDto = new AuthResponseDto(
            AccessToken: accessToken,
            ExpiresAtUtc: expiresAtUtc,
            User: new UserProfileDto(userId, email, displayName, roles.ToList())
        );

        return (responseDto, rawRefreshToken);
    }

    public async Task<(AuthResponseDto Response, string NewRawRefreshToken)> RefreshTokenAsync(
        string rawRefreshToken,
        string? deviceLabel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        var tokenHash = _jwtTokenService.HashToken(rawRefreshToken);
        var existingToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (existingToken.IsRevoked)
        {
            // Possible token reuse attack: revoke all tokens for this user
            var userTokens = await _context.RefreshTokens
                .Where(t => t.UserId == existingToken.UserId && t.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var t in userTokens)
            {
                t.Revoke(DateTime.UtcNow);
            }
            await _context.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedAccessException("Revoked refresh token reuse detected. All sessions revoked.");
        }

        if (existingToken.IsExpired)
        {
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        var isActive = await _identityService.IsUserActiveAsync(existingToken.UserId, cancellationToken);
        if (!isActive)
        {
            existingToken.Revoke(DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Account is deactivated.");
        }

        var userInfo = await _identityService.GetUserByIdAsync(existingToken.UserId, cancellationToken);
        if (userInfo == null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var (newAccessToken, newExpiresAtUtc) = _jwtTokenService.GenerateAccessToken(
            userInfo.Value.UserId,
            userInfo.Value.Email,
            userInfo.Value.DisplayName,
            userInfo.Value.Roles);

        var (newRawToken, newTokenHash, newRefreshExpiresAtUtc) = _jwtTokenService.GenerateRefreshToken();

        var newRefreshTokenEntity = new RefreshToken(
            userInfo.Value.UserId,
            newTokenHash,
            newRefreshExpiresAtUtc,
            deviceLabel ?? existingToken.DeviceLabel);

        existingToken.Revoke(DateTime.UtcNow, newRefreshTokenEntity.Id);

        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var responseDto = new AuthResponseDto(
            AccessToken: newAccessToken,
            ExpiresAtUtc: newExpiresAtUtc,
            User: new UserProfileDto(userInfo.Value.UserId, userInfo.Value.Email, userInfo.Value.DisplayName, userInfo.Value.Roles.ToList())
        );

        return (responseDto, newRawToken);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return;

        var tokenHash = _jwtTokenService.HashToken(rawRefreshToken);
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token != null && token.IsActive)
        {
            token.Revoke(DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserProfileDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId == null)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var userInfo = await _identityService.GetUserByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (userInfo == null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        return new UserProfileDto(
            userInfo.Value.UserId,
            userInfo.Value.Email,
            userInfo.Value.DisplayName,
            userInfo.Value.Roles.ToList()
        );
    }
}
