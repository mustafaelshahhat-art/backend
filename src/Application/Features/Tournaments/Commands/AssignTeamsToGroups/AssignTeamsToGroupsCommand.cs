using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.AssignTeamsToGroups;

public record AssignTeamsToGroupsCommand(
    Guid TournamentId,
    List<GroupAssignmentDto> Assignments,
    Guid UserId,
    string UserRole) : IRequest<Unit>;
