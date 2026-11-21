namespace WorkFlowPro.Application.Features.Auth.Dtos;

public record LoginRequest(
    string Email,
    string Password
);
