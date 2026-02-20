using Application.DTOs.Teams;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;
using System.Linq.Expressions;

namespace Application.Features.Teams.Commands.AddGuestPlayer;

public class AddGuestPlayerCommandHandler : IRequestHandler<AddGuestPlayerCommand, PlayerDto>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<Player> _playerRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _realTimeNotifier;

    public AddGuestPlayerCommandHandler(
        IRepository<Team> teamRepository, IRepository<Player> playerRepository,
        IMapper mapper, IRealTimeNotifier realTimeNotifier)
    {
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
    }

    public async Task<PlayerDto> Handle(AddGuestPlayerCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Request.Name))
            throw new BadRequestException("اسم اللاعب مطلوب.");

        var team = await _teamRepository.GetByIdAsync(request.TeamId,
            new Expression<Func<Team, object>>[] { t => t.Players }, ct);
        if (team == null) throw new NotFoundException(nameof(Team), request.TeamId);

        var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain == null || captain.UserId != request.CaptainId)
            throw new ForbiddenException("فقط قائد الفريق يمكنه إضافة لاعبين.");

        if (team.Players.Any(p => p.Name.Trim().Equals(request.Request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException("يوجد لاعب بنفس الاسم في الفريق بالفعل.");

        var player = new Player
        {
            Name = request.Request.Name.Trim(),
            DisplayId = "G-" + new Random().Next(100000, 999999),
            UserId = null, TeamId = request.TeamId,
            TeamRole = TeamRole.Member,
            Number = request.Request.Number ?? 0,
            Position = request.Request.Position ?? "لاعب",
            Status = "active"
        };
        await _playerRepository.AddAsync(player, ct);

        // Broadcast team snapshot
        var teamSnapshot = await _teamRepository.GetByIdNoTrackingAsync(request.TeamId,
            new Expression<Func<Team, object>>[] { t => t.Players, t => t.Statistics! }, ct);
        if (teamSnapshot != null)
            await _realTimeNotifier.SendTeamUpdatedAsync(_mapper.Map<TeamDto>(teamSnapshot), ct);

        return _mapper.Map<PlayerDto>(player);
    }
}
