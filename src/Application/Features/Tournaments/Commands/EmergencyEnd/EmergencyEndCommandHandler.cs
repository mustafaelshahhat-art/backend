using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.EmergencyEnd;

public class EmergencyEndCommandHandler : IRequestHandler<EmergencyEndCommand, TournamentDto>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IMapper _mapper;

    public EmergencyEndCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _mapper = mapper;
    }

    public async Task<TournamentDto> Handle(EmergencyEndCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), request.Id);

        // Authorization
        var isAdmin = request.UserRole == UserRole.Admin.ToString();
        var isOwner = request.UserRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == request.UserId;
        if (!isAdmin && !isOwner) throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

        // Guarded Transition
        tournament.ChangeStatus(TournamentStatus.Completed);

        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
        
        return _mapper.Map<TournamentDto>(tournament);
    }
}
