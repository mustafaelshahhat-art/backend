using Application.DTOs.Tournaments;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Tournaments.Commands.SubmitPayment;

public class SubmitPaymentCommandHandler : IRequestHandler<SubmitPaymentCommand, TeamRegistrationDto>
{
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly AutoMapper.IMapper _mapper;

    public SubmitPaymentCommandHandler(
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Team> teamRepository,
        AutoMapper.IMapper mapper)
    {
        _registrationRepository = registrationRepository;
        _teamRepository = teamRepository;
        _mapper = mapper;
    }

    public async Task<TeamRegistrationDto> Handle(SubmitPaymentCommand request, CancellationToken cancellationToken)
    {
        var registration = (await _registrationRepository.FindAsync(r => 
            r.TournamentId == request.TournamentId && r.TeamId == request.TeamId, cancellationToken)).FirstOrDefault();
        
        if (registration == null) 
            throw new NotFoundException("لا يوجد تسجيل لهذا الفريق.");

        var team = await _teamRepository.GetByIdAsync(request.TeamId, new[] { "Players" }, cancellationToken);
        if (team == null) 
            throw new NotFoundException(nameof(Team), request.TeamId);

        if (!team.Players.Any(p => p.UserId == request.UserId && p.TeamRole == TeamRole.Captain)) 
            throw new ForbiddenException("غير مصرح لك. فقط كابتن الفريق يمكنه تقديم طلب الدفع.");

        registration.PaymentReceiptUrl = request.PaymentReceiptUrl;
        registration.SenderNumber = request.SenderNumber;
        registration.PaymentMethod = request.PaymentMethod;
        registration.Status = RegistrationStatus.PendingPaymentReview;
        registration.UpdatedAt = DateTime.UtcNow;

        await _registrationRepository.UpdateAsync(registration, cancellationToken);
        
        return _mapper.Map<TeamRegistrationDto>(registration);
    }
}
