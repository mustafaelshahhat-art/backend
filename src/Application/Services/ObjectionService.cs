using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Objections;
using Application.Interfaces;
using Application.Common;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Shared.Exceptions;
using System.Linq;

namespace Application.Services;

public class ObjectionService : IObjectionService
{
    private readonly IRepository<Objection> _objectionRepository;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;
    private readonly IRealTimeNotifier _notifier;
    private readonly IAnalyticsService _analyticsService;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<Match> _matchRepository;

    public ObjectionService(
        IRepository<Objection> objectionRepository, 
        IMapper mapper, 
        INotificationService notificationService, 
        IRealTimeNotifier notifier,
        IAnalyticsService analyticsService,
        IRepository<Team> teamRepository,
        IRepository<Match> matchRepository)
    {
        _objectionRepository = objectionRepository;
        _mapper = mapper;
        _notificationService = notificationService;
        _notifier = notifier;
        _analyticsService = analyticsService;
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
    }

    public async Task<IEnumerable<ObjectionDto>> GetAllAsync(Guid? creatorId = null)
    {
        var objections = await _objectionRepository.FindAsync(
            o => !creatorId.HasValue || o.Match!.Tournament!.CreatorUserId == creatorId.Value, 
            new[] { "Team.Players", "Match.Tournament", "Match.HomeTeam", "Match.AwayTeam" }
        );
        return _mapper.Map<IEnumerable<ObjectionDto>>(objections);
    }

    public async Task<IEnumerable<ObjectionDto>> GetByTeamIdAsync(Guid teamId)
    {
        var objections = await _objectionRepository.FindAsync(o => o.TeamId == teamId, new[] { "Team.Players", "Match.Tournament", "Match.HomeTeam", "Match.AwayTeam" });
        return _mapper.Map<IEnumerable<ObjectionDto>>(objections);
    }

    public async Task<ObjectionDto?> GetByIdAsync(Guid id, Guid userId, string userRole)
    {
        var objections = await _objectionRepository.FindAsync(o => o.Id == id, new[] { "Team.Players", "Match.Tournament", "Match.HomeTeam", "Match.AwayTeam" });
        var objection = objections.FirstOrDefault();
        if (objection == null) return null;

        var isAdmin = userRole == UserRole.Admin.ToString();
        var isCreator = userRole == UserRole.TournamentCreator.ToString() && objection.Match?.Tournament?.CreatorUserId == userId;
        var isCaptain = objection.TeamId != Guid.Empty && (await _teamRepository.GetByIdAsync(objection.TeamId, t => t.Players))?.Players.Any(p => p.UserId == userId && p.TeamRole == TeamRole.Captain) == true;

        if (!isAdmin && !isCreator && !isCaptain)
        {
            throw new ForbiddenException("غير مصرح لك بعرض هذا الاعتراض.");
        }

        return _mapper.Map<ObjectionDto>(objection);
    }

    public async Task<ObjectionDto> SubmitAsync(SubmitObjectionRequest request, Guid teamId)
    {
        if (!Enum.TryParse<ObjectionType>(request.Type, true, out var type))
        {
            // Try matching normalized uppercase strings if PascalCase fails
            var normalizedTypes = Enum.GetNames<ObjectionType>().ToDictionary(t => t.ToUpperInvariant(), t => Enum.Parse<ObjectionType>(t));
            if (normalizedTypes.TryGetValue(request.Type.ToUpperInvariant().Replace("_", ""), out var fallbackType))
            {
                type = fallbackType;
            }
            else
            {
                throw new BadRequestException($"نوع الاعتراض غير صالح: {request.Type}");
            }
        }

        var objection = new Objection
        {
            MatchId = request.MatchId,
            TeamId = teamId,
            Type = type,
            Description = request.Description,
            Status = ObjectionStatus.Pending
        };

        await _objectionRepository.AddAsync(objection);
        var dto = _mapper.Map<ObjectionDto>(objection);
        
        // Broadcast real-time event
        await _notifier.SendObjectionSubmittedAsync(dto);
        
        // Persistent Notification for Admins
        await _notificationService.SendNotificationByTemplateAsync(Guid.Empty, NotificationTemplates.OBJECTION_SUBMITTED, new Dictionary<string, string> { { "matchId", request.MatchId.ToString() } }, "objection");
        
        // Log Activity
        var team = await _teamRepository.GetByIdAsync(teamId);
        var match = await _matchRepository.GetByIdAsync(request.MatchId, new[] { "HomeTeam", "AwayTeam" });

        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.OBJECTION_SUBMITTED, 
            new Dictionary<string, string> { 
                { "matchInfo", $"{match?.HomeTeam?.Name ?? "فريق"} ضد {match?.AwayTeam?.Name ?? "فريق"}" },
                { "teamName", team?.Name ?? "فريق" }
            }, 
            null, 
            "فريق"
        );

        return dto;
    }

    public async Task<ObjectionDto> ResolveAsync(Guid id, ResolveObjectionRequest request, Guid userId, string userRole)
    {
        var objection = (await _objectionRepository.FindAsync(o => o.Id == id, new[] { "Match.Tournament" })).FirstOrDefault();
        if (objection == null) throw new NotFoundException(nameof(Objection), id);

        var tournamentCreatorId = objection.Match?.Tournament?.CreatorUserId;

        // Authorization: Admin or the specific Tournament Creator
        var isAdmin = userRole == UserRole.Admin.ToString();
        var isOwner = userRole == UserRole.TournamentCreator.ToString() && tournamentCreatorId == userId;

        if (!isAdmin && !isOwner)
        {
             throw new ForbiddenException("غير مصرح لك بإدارة هذا الاعتراض. فقط منظم البطولة أو مدير النظام يمكنه ذلك.");
        }

        // State Validation
        if (objection.Status != ObjectionStatus.Pending)
        {
             throw new ConflictException($"لا يمكن تغيير حالة الاعتراض. الحالة الحالية: {objection.Status}");
        }

        objection.Status = request.Approved ? ObjectionStatus.Approved : ObjectionStatus.Rejected;
        if (!string.IsNullOrEmpty(request.Notes))
        {
            objection.AdminNotes = request.Notes;
        }

        await _objectionRepository.UpdateAsync(objection);
        var dto = _mapper.Map<ObjectionDto>(objection);

        // Broadcast real-time event
        await _notifier.SendObjectionResolvedAsync(dto);

        // Persistent Notification for Captain
        // Fetch objection with Team and Players.
        var fullObjection = await _objectionRepository.FindAsync(o => o.Id == id, new[] { "Team.Players" });
        var targetTeam = fullObjection.FirstOrDefault()?.Team;
        if (targetTeam != null)
        {
            var captain = targetTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, 
                    request.Approved ? NotificationTemplates.OBJECTION_APPROVED : NotificationTemplates.OBJECTION_REJECTED,
                    new Dictionary<string, string> { { "matchId", objection.MatchId.ToString() } }, 
                    "objection");
            }
        }

        // Log Activity
        var matchObj = await _matchRepository.GetByIdAsync(objection.MatchId, new[] { "HomeTeam", "AwayTeam" });

        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.OBJECTION_RESOLVED, 
            new Dictionary<string, string> { 
                { "matchInfo", $"{matchObj?.HomeTeam?.Name ?? "فريق"} ضد {matchObj?.AwayTeam?.Name ?? "فريق"}" },
                { "resolution", request.Approved ? "مقبول" : "مرفوض" }
            }, 
            null, 
            "إدارة"
        );

        return dto;
    }
}
