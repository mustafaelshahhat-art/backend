using Application.DTOs.Teams;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Teams.Commands.CreateTeam;

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, TeamDto>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly ISystemSettingsService _systemSettingsService;

    public CreateTeamCommandHandler(
        IRepository<Team> teamRepository, IRepository<User> userRepository,
        IMapper mapper, IRealTimeNotifier realTimeNotifier,
        ISystemSettingsService systemSettingsService)
    {
        _teamRepository = teamRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
        _systemSettingsService = systemSettingsService;
    }

    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken ct)
    {
        if (!await _systemSettingsService.IsTeamCreationAllowedAsync(ct))
            throw new Shared.Exceptions.BadRequestException("إنشاء الفرق متوقف حالياً");

        var captain = await _userRepository.GetByIdAsync(request.CaptainId, ct);
        if (captain == null) throw new Shared.Exceptions.NotFoundException(nameof(User), request.CaptainId);

        var team = new Team
        {
            Name = request.Request.Name,
            Founded = request.Request.Founded,
            City = request.Request.City,
            Players = new List<Player>()
        };

        var player = new Player
        {
            Name = captain.Name,
            DisplayId = "P-" + captain.DisplayId.Replace("U-", ""),
            UserId = request.CaptainId,
            TeamRole = TeamRole.Captain
        };
        team.Players.Add(player);

        await _teamRepository.AddAsync(team, ct);

        if (!captain.TeamId.HasValue)
        {
            captain.TeamId = team.Id;
            await _userRepository.UpdateAsync(captain, ct);
        }

        var dto = _mapper.Map<TeamDto>(team);
        dto.CaptainName = captain.Name;
        await _realTimeNotifier.SendTeamCreatedAsync(dto, ct);
        return dto;
    }
}
