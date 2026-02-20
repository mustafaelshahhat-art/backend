using Application.DTOs.Teams;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Teams.Commands.UpdateTeam;

public class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand, TeamDto>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _realTimeNotifier;

    public UpdateTeamCommandHandler(IRepository<Team> teamRepository, IMapper mapper, IRealTimeNotifier realTimeNotifier)
    {
        _teamRepository = teamRepository;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
    }

    public async Task<TeamDto> Handle(UpdateTeamCommand request, CancellationToken ct)
    {
        await TeamAuthorizationHelper.ValidateManagementRights(_teamRepository, request.Id, request.UserId, request.UserRole, ct);
        var team = await _teamRepository.GetByIdAsync(request.Id, ct);
        if (team == null) throw new NotFoundException(nameof(Team), request.Id);

        if (!string.IsNullOrEmpty(request.Request.Name)) team.Name = request.Request.Name!;
        if (!string.IsNullOrEmpty(request.Request.City)) team.City = request.Request.City;
        if (request.Request.IsActive.HasValue) team.IsActive = request.Request.IsActive.Value;

        await _teamRepository.UpdateAsync(team, ct);
        var dto = _mapper.Map<TeamDto>(team);
        await _realTimeNotifier.SendTeamUpdatedAsync(dto, ct);
        return dto;
    }
}
