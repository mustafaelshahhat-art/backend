using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.SubmitPayment;

public class SubmitPaymentCommandHandler : IRequestHandler<SubmitPaymentCommand, TeamRegistrationDto>
{
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IMapper _mapper;

    public SubmitPaymentCommandHandler(
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Team> teamRepository,
        IFileStorageService fileStorage,
        IMapper mapper)
    {
        _registrationRepository = registrationRepository;
        _teamRepository = teamRepository;
        _fileStorage = fileStorage;
        _mapper = mapper;
    }

    public async Task<TeamRegistrationDto> Handle(SubmitPaymentCommand request, CancellationToken cancellationToken)
    {
        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == request.TournamentId && r.TeamId == request.TeamId, cancellationToken)).FirstOrDefault();
        if (registration == null) throw new NotFoundException("لا يوجد تسجيل لهذا الفريق.");

        var team = await _teamRepository.GetByIdAsync(request.TeamId, new[] { "Players" }, cancellationToken);
        if (team == null) throw new NotFoundException(nameof(Team), request.TeamId);
        
        // Authorization: Only captain
        if (!team.Players.Any(p => p.UserId == request.UserId && p.TeamRole == TeamRole.Captain)) 
        {
            throw new ForbiddenException("فقط كابتن الفريق يمكنه رفع إيصال الدفع.");
        }

        var receiptUrl = await _fileStorage.SaveFileAsync(request.FileStream, request.FileName, request.ContentType, cancellationToken);

        registration.PaymentReceiptUrl = receiptUrl;
        registration.SenderNumber = request.SenderNumber;
        registration.PaymentMethod = request.PaymentMethod;
        registration.Status = RegistrationStatus.PendingPaymentReview;
        
        await _registrationRepository.UpdateAsync(registration, cancellationToken);
        
        return _mapper.Map<TeamRegistrationDto>(registration);
    }
}
