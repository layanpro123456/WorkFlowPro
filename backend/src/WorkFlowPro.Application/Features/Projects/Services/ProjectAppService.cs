using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Projects.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Application.Features.Projects.Services;

public class ProjectAppService : IProjectAppService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly IValidator<CreateProjectRequest> _createValidator;
    private readonly IValidator<UpdateProjectRequest> _updateValidator;
    private readonly IValidator<AddProjectMemberRequest> _addMemberValidator;

    public ProjectAppService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        IValidator<CreateProjectRequest> createValidator,
        IValidator<UpdateProjectRequest> updateValidator,
        IValidator<AddProjectMemberRequest> addMemberValidator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addMemberValidator = addMemberValidator;
    }

    public async Task<ProjectSummaryDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();

        var codeNormalized = request.ProjectCode.Trim().ToUpperInvariant();
        var codeExists = await _context.Projects.AnyAsync(p => p.ProjectCode == codeNormalized, cancellationToken);
        if (codeExists)
        {
            throw new DomainException($"A project with code '{codeNormalized}' already exists.");
        }

        var project = new Project(
            projectCode: codeNormalized,
            name: request.Name,
            description: request.Description,
            createdById: callerId,
            startDate: request.StartDate,
            targetDate: request.TargetDate
        );

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);

        return new ProjectSummaryDto(
            Id: project.Id,
            ProjectCode: project.ProjectCode,
            Name: project.Name,
            Status: project.Status,
            StartDate: project.StartDate,
            TargetDate: project.TargetDate,
            MemberCount: 1,
            CreatedAtUtc: project.CreatedAtUtc
        );
    }

    public async Task<PagedResult<ProjectSummaryDto>> GetPagedListAsync(
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isAdmin = _currentUserService.Roles.Contains(Roles.Administrator);

        var query = _context.Projects
            .Include(p => p.Members)
            .AsNoTracking();

        // Non-admins can only see projects where they are members
        if (!isAdmin)
        {
            query = query.Where(p => p.Members.Any(m => m.UserId == callerId));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchClean = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchClean) || p.ProjectCode.ToLower().Contains(searchClean));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)safePageSize);

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(p => new ProjectSummaryDto(
                p.Id,
                p.ProjectCode,
                p.Name,
                p.Status,
                p.StartDate,
                p.TargetDate,
                p.Members.Count,
                p.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProjectSummaryDto>(items, safePage, safePageSize, totalCount, totalPages);
    }

    public async Task<ProjectDetailDto?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isAdmin = _currentUserService.Roles.Contains(Roles.Administrator);

        var project = await _context.Projects
            .Include(p => p.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null) return null;

        // Conceal resource if non-member and non-admin
        if (!isAdmin && !project.Members.Any(m => m.UserId == callerId))
        {
            return null;
        }

        var creatorInfo = await _identityService.GetUserByIdAsync(project.CreatedById, cancellationToken);
        var creatorName = creatorInfo?.DisplayName ?? "Unknown User";

        var memberDtos = new List<ProjectMemberDto>();
        foreach (var member in project.Members)
        {
            var userInfo = await _identityService.GetUserByIdAsync(member.UserId, cancellationToken);
            memberDtos.Add(new ProjectMemberDto(
                UserId: member.UserId,
                DisplayName: userInfo?.DisplayName ?? "Unknown User",
                Email: userInfo?.Email ?? string.Empty,
                ProjectRole: member.ProjectRole,
                JoinedAtUtc: member.JoinedAtUtc
            ));
        }

        return new ProjectDetailDto(
            Id: project.Id,
            ProjectCode: project.ProjectCode,
            Name: project.Name,
            Description: project.Description,
            Status: project.Status,
            StartDate: project.StartDate,
            TargetDate: project.TargetDate,
            CreatedById: project.CreatedById,
            CreatedByName: creatorName,
            CreatedAtUtc: project.CreatedAtUtc,
            ArchivedAtUtc: project.ArchivedAtUtc,
            Members: memberDtos
        );
    }

    public async Task<ProjectDetailDto> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();
        var isAdmin = _currentUserService.Roles.Contains(Roles.Administrator);

        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project {projectId} not found.");
        }

        // Check update permission: Admin or Project Owner/Manager
        var callerMembership = project.Members.FirstOrDefault(m => m.UserId == callerId);
        var canUpdate = isAdmin || (callerMembership != null &&
            (callerMembership.ProjectRole == ProjectRoles.Owner || callerMembership.ProjectRole == ProjectRoles.Manager));

        if (!canUpdate)
        {
            throw new UnauthorizedAccessException("You do not have permission to update this project.");
        }

        project.UpdateDetails(request.Name, request.Description, request.StartDate, request.TargetDate, request.Status);
        await _context.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(projectId, cancellationToken))!;
    }

    public async Task ArchiveAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isAdmin = _currentUserService.Roles.Contains(Roles.Administrator);

        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project {projectId} not found.");
        }

        var callerMembership = project.Members.FirstOrDefault(m => m.UserId == callerId);
        var canArchive = isAdmin || (callerMembership != null && callerMembership.ProjectRole == ProjectRoles.Owner);

        if (!canArchive)
        {
            throw new UnauthorizedAccessException("Only project owners or administrators can archive projects.");
        }

        project.Archive(DateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectMemberDto>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var detail = await GetByIdAsync(projectId, cancellationToken);
        if (detail == null)
        {
            throw new KeyNotFoundException($"Project {projectId} not found or inaccessible.");
        }
        return detail.Members;
    }

    public async Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _addMemberValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();
        var isAdmin = _currentUserService.Roles.Contains(Roles.Administrator);

        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project {projectId} not found.");
        }

        var callerMembership = project.Members.FirstOrDefault(m => m.UserId == callerId);
        var canManageMembers = isAdmin || (callerMembership != null &&
            (callerMembership.ProjectRole == ProjectRoles.Owner || callerMembership.ProjectRole == ProjectRoles.Manager));

        if (!canManageMembers)
        {
            throw new UnauthorizedAccessException("You do not have permission to manage members of this project.");
        }

        var targetUser = await _identityService.GetUserByIdAsync(request.UserId, cancellationToken);
        if (targetUser == null)
        {
            throw new DomainException("Target user does not exist.");
        }

        var isActive = await _identityService.IsUserActiveAsync(request.UserId, cancellationToken);
        if (!isActive)
        {
            throw new DomainException("Cannot add an inactive user to a project.");
        }

        project.AddMember(request.UserId, request.ProjectRole);
        await _context.SaveChangesAsync(cancellationToken);

        var newMember = project.Members.First(m => m.UserId == request.UserId);
        return new ProjectMemberDto(
            UserId: newMember.UserId,
            DisplayName: targetUser.Value.DisplayName,
            Email: targetUser.Value.Email,
            ProjectRole: newMember.ProjectRole,
            JoinedAtUtc: newMember.JoinedAtUtc
        );
    }

    public async Task RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isAdmin = _currentUserService.Roles.Contains(Roles.Administrator);

        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project {projectId} not found.");
        }

        var callerMembership = project.Members.FirstOrDefault(m => m.UserId == callerId);
        var canManageMembers = isAdmin || (callerMembership != null && callerMembership.ProjectRole == ProjectRoles.Owner);

        if (!canManageMembers && callerId != userId) // Allow member to remove themselves (leave project)
        {
            throw new UnauthorizedAccessException("You do not have permission to remove members from this project.");
        }

        project.RemoveMember(userId);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireCallerId()
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId == null)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }
        return _currentUserService.UserId.Value;
    }
}
