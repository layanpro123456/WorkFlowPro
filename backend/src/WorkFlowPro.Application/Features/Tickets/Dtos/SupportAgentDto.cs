namespace WorkFlowPro.Application.Features.Tickets.Dtos;

public record SupportAgentDto(
    Guid UserId,
    string DisplayName,
    string Email,
    string PrimaryRole
);
