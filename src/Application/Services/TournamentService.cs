using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Shared.Exceptions;

namespace Application.Services;

public class TournamentService : ITournamentService
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IMapper _mapper;
    private readonly IAnalyticsService _analyticsService;
    private readonly INotificationService _notificationService;
    private readonly IRepository<Team> _teamRepository; // Need Team repo to get CaptainId

    public TournamentService(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IMapper mapper,
        IAnalyticsService analyticsService,
        INotificationService notificationService,
        IRepository<Team> teamRepository)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _mapper = mapper;
        _analyticsService = analyticsService;
        _notificationService = notificationService;
        _teamRepository = teamRepository;
    }

    public async Task<IEnumerable<TournamentDto>> GetAllAsync()
    {
        var tournaments = await _tournamentRepository.GetAllAsync(new[] { "Registrations", "Registrations.Team", "Registrations.Team.Captain" });
        return _mapper.Map<IEnumerable<TournamentDto>>(tournaments);
    }

    public async Task<TournamentDto?> GetByIdAsync(Guid id)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id, new[] { "Registrations", "Registrations.Team", "Registrations.Team.Captain" });
        return tournament == null ? null : _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<TournamentDto> CreateAsync(CreateTournamentRequest request)
    {
        var tournament = new Tournament
        {
            Name = request.Name,
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RegistrationDeadline = request.RegistrationDeadline,
            EntryFee = request.EntryFee,
            MaxTeams = request.MaxTeams,
            Location = request.Location,
            Rules = request.Rules,
            Prizes = request.Prizes,
            Status = "registration_open"
        };

        await _tournamentRepository.AddAsync(tournament);
        await _analyticsService.LogActivityAsync("Tournament Created", $"Tournament {tournament.Name} created.", null, "Admin");
        return _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<TournamentDto> UpdateAsync(Guid id, UpdateTournamentRequest request)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        if (request.Name != null) tournament.Name = request.Name;
        if (request.Description != null) tournament.Description = request.Description;
        if (request.Status != null) tournament.Status = request.Status;
        if (request.StartDate.HasValue) tournament.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) tournament.EndDate = request.EndDate.Value;
        if (request.RegistrationDeadline.HasValue) tournament.RegistrationDeadline = request.RegistrationDeadline.Value;
        if (request.EntryFee.HasValue) tournament.EntryFee = request.EntryFee.Value;
        if (request.MaxTeams.HasValue) tournament.MaxTeams = request.MaxTeams.Value;
        if (request.Location != null) tournament.Location = request.Location;
        if (request.Rules != null) tournament.Rules = request.Rules;
        if (request.Prizes != null) tournament.Prizes = request.Prizes;

        await _tournamentRepository.UpdateAsync(tournament);
        return _mapper.Map<TournamentDto>(tournament);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _tournamentRepository.DeleteAsync(id);
    }

    public async Task<TeamRegistrationDto> RegisterTeamAsync(Guid tournamentId, RegisterTeamRequest request)
    {
        // Check if tournament exists
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        // Check if team is already registered in THIS tournament
        var existing = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == request.TeamId);
        if (existing.Any()) throw new ConflictException("الفريق مسجل بالفعل في هذه البطولة.");

        // Check capacity (include pending review)
        var regCount = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.Status != RegistrationStatus.Rejected)).Count();
        if (regCount >= tournament.MaxTeams) throw new ConflictException("عذراً، اكتمل العدد الأقصى للفرق في هذه البطولة.");

        // Check if team is registered in ANY other active tournament
        var allRegistrations = await _registrationRepository.FindAsync(r => r.TeamId == request.TeamId);
        foreach (var reg in allRegistrations)
        {
            if (reg.Status == RegistrationStatus.Rejected) continue;

            var t = await _tournamentRepository.GetByIdAsync(reg.TournamentId);
            if (t != null && t.Status != "completed")
            {
                throw new ConflictException($"الفريق مسجل بالفعل في بطولة أخرى جارية: {t.Name}");
            }
        }

        var registration = new TeamRegistration
        {
            TournamentId = tournamentId,
            TeamId = request.TeamId,
            Status = RegistrationStatus.PendingPaymentReview
        };

        await _registrationRepository.AddAsync(registration);
        
        tournament.CurrentTeams++;
        await _tournamentRepository.UpdateAsync(tournament);
        
        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    public async Task<IEnumerable<TeamRegistrationDto>> GetRegistrationsAsync(Guid tournamentId)
    {
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId);
        return _mapper.Map<IEnumerable<TeamRegistrationDto>>(registrations);
    }

    public async Task<TeamRegistrationDto> SubmitPaymentAsync(Guid tournamentId, Guid teamId, SubmitPaymentRequest request)
    {
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId);
        var reg = registrations.FirstOrDefault();
        if (reg == null) throw new NotFoundException("Registration not found.");

        reg.PaymentReceiptUrl = request.PaymentReceiptUrl;
        // Status remains PendingPaymentReview as per rules
        
        await _registrationRepository.UpdateAsync(reg);
        
        // Notify Admins
        await _notificationService.SendNotificationAsync(Guid.Empty, "New payment awaiting review", $"New payment receipt submitted for Team {reg.TeamId} in Tournament {tournamentId}", "admin_broadcast");
        return _mapper.Map<TeamRegistrationDto>(reg);
    }

    public async Task<TeamRegistrationDto> ApproveRegistrationAsync(Guid tournamentId, Guid teamId)
    {
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId);
        var reg = registrations.FirstOrDefault();
        if (reg == null) throw new NotFoundException("Registration not found.");

        reg.Status = RegistrationStatus.Approved;
        
        await _registrationRepository.UpdateAsync(reg);
        await _analyticsService.LogActivityAsync("Registration Approved", $"Team ID {teamId} approved for Tournament ID {tournamentId}", null, "Admin");
        
        // Notify Captain
        var team = await _teamRepository.GetByIdAsync(teamId);
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (team != null) await _notificationService.SendNotificationAsync(team.CaptainId, "Registration Approved", $"Your registration for {tournament?.Name} has been approved.", "system");

        return _mapper.Map<TeamRegistrationDto>(reg);
    }

    public async Task<TeamRegistrationDto> RejectRegistrationAsync(Guid tournamentId, Guid teamId, RejectRegistrationRequest request)
    {
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId);
        var reg = registrations.FirstOrDefault();
        if (reg == null) throw new NotFoundException("Registration not found.");

        reg.Status = RegistrationStatus.Rejected;
        reg.RejectionReason = request.Reason;

        await _registrationRepository.UpdateAsync(reg);
        
        // Decrement capacity
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament != null)
        {
            tournament.CurrentTeams = Math.Max(0, tournament.CurrentTeams - 1);
            await _tournamentRepository.UpdateAsync(tournament);
        }
        
        // Notify Captain
        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team != null) await _notificationService.SendNotificationAsync(team.CaptainId, "Registration Rejected", $"Your registration for {tournament?.Name} was rejected: {request.Reason}", "system");

        return _mapper.Map<TeamRegistrationDto>(reg);
    }

    public async Task<IEnumerable<PendingPaymentResponse>> GetPendingPaymentsAsync()
    {
        var pending = await _registrationRepository.FindAsync(r => r.Status == RegistrationStatus.PendingPaymentReview && !string.IsNullOrEmpty(r.PaymentReceiptUrl));
        
        var result = new List<PendingPaymentResponse>();
        foreach (var p in pending)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(p.TournamentId);
            // Ideally load relations efficiently.
            
            result.Add(new PendingPaymentResponse
            {
                Registration = _mapper.Map<TeamRegistrationDto>(p),
                Tournament = _mapper.Map<TournamentDto>(tournament!) // Assuming tournament exists integrity
            });
        }
        return result;
    }
}
