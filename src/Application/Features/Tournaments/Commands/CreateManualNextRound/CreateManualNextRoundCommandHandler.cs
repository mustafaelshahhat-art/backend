using Application.DTOs;
using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.CreateManualNextRound;

/// <summary>
/// Handles organiser-supplied pairings for the next knockout round in a Manual-mode tournament.
///
/// DESIGN NOTES
/// ────────────
/// • Calls tournament.RequiresManualDraw to enforce the domain policy — no policy logic here.
/// • Delegates all match-entity creation to MatchGenerator.GenerateManualKnockout —
///   the same engine used by CreateManualKnockoutMatchesCommandHandler.
/// • &lt;= 5 constructor dependencies.
/// • Handler body &lt;= 100 lines.
/// </summary>
public class CreateManualNextRoundCommandHandler
    : IRequestHandler<CreateManualNextRoundCommand, MatchListResponse>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedLock _distributedLock;
    private readonly IRealTimeNotifier _notifier;

    public CreateManualNextRoundCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IMapper mapper,
        IDistributedLock distributedLock,
        IRealTimeNotifier notifier)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
        _distributedLock = distributedLock;
        _notifier = notifier;
    }

    public async Task<MatchListResponse> Handle(
        CreateManualNextRoundCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-matches-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(15), cancellationToken))
            throw new ConflictException("يتم معالجة مباريات هذه البطولة من قبل مستخدم آخر حالياً.");

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(
                request.TournamentId, cancellationToken);
            if (tournament == null)
                throw new NotFoundException(nameof(Tournament), request.TournamentId);

            // ── Authorization ──
            var isAdmin = request.UserRole == UserRole.Admin.ToString();
            var isOwner = request.UserRole == UserRole.TournamentCreator.ToString()
                          && tournament.CreatorUserId == request.UserId;
            if (!isAdmin && !isOwner)
                throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

            if (tournament.Status != TournamentStatus.Active && tournament.Status != TournamentStatus.QualificationConfirmed)
                throw new BadRequestException("يجب أن تكون البطولة نشطة أو في مرحلة تأكيد التأهل لإضافة جولة جديدة.");

            // ── Domain rule: Manual Draw Policy ──
            // For QualificationConfirmed tournaments the organiser is always submitting
            // the *first* knockout round, never the Final — even if only 2 teams qualified.
            // isFinalRound is only relevant for pure-knockout tournaments that are mid-run.
            bool isFinalRound = request.Pairings.Count == 1
                                 && tournament.Status != TournamentStatus.QualificationConfirmed;
            if (!tournament.RequiresManualDraw(request.RoundNumber, isFinalRound))
                throw new BadRequestException(
                    isFinalRound
                        ? "المباراة النهائية يتم توليدها تلقائياً. لا يمكن إدخالها يدوياً."
                        : "هذه البطولة تستخدم التوليد التلقائي للجولات. استخدم التوليد التلقائي بدلاً من ذلك.");

            // ── Guard: previous knockout round must be complete ──
            // Skip this check for round 1 when coming from QualificationConfirmed — there is
            // no previous knockout round; the preceding matches were group-stage matches.
            if (request.RoundNumber > 1 || tournament.Status == TournamentStatus.Active)
            {
                var prevRoundMatches = await _matchRepository.FindAsync(
                    m => m.TournamentId == request.TournamentId
                         && m.RoundNumber == request.RoundNumber - 1,
                    cancellationToken);
                if (prevRoundMatches.Any(m => m.Status != MatchStatus.Finished))
                    throw new BadRequestException("لم تنتهِ جميع مباريات الجولة السابقة بعد.");
            }

            // ── Guard: this round must not already exist (knockout only — exclude group matches) ──
            // Group-stage matches can share the same RoundNumber (e.g. both are "Round 1").
            // We only care about knockout matches here, so filter by GroupId == null.
            // Exception: when the tournament is QualificationConfirmed and the organiser is
            // submitting round 1, any existing knockout matches were auto-generated by the old
            // code path. We allow replacement by deleting them first.
            var existingRoundMatches = await _matchRepository.FindAsync(
                m => m.TournamentId == request.TournamentId
                     && m.RoundNumber == request.RoundNumber
                     && m.GroupId == null,      // ← only knockout matches
                cancellationToken);

            if (existingRoundMatches.Any())
            {
                bool canReplace = tournament.Status == TournamentStatus.QualificationConfirmed
                                  && request.RoundNumber == 1
                                  && existingRoundMatches.All(m => m.Status == MatchStatus.Scheduled);

                if (!canReplace)
                    throw new ConflictException($"مباريات الجولة {request.RoundNumber} موجودة بالفعل.");

                // Delete stale auto-generated matches so the organiser can draw manually.
                await _matchRepository.DeleteRangeAsync(existingRoundMatches, cancellationToken);
            }

            // ── Pairing validation ──
            if (!request.Pairings.Any())
                throw new BadRequestException("يجب تحديد مواجهة واحدة على الأقل.");

            var participantIds = request.Pairings
                .SelectMany(p => new[] { p.HomeTeamId, p.AwayTeamId }).ToList();

            if (participantIds.Count != participantIds.Distinct().Count())
                throw new BadRequestException("لا يمكن تكرار الفريق في المواجهات.");

            if (request.Pairings.Any(p => p.HomeTeamId == p.AwayTeamId))
                throw new BadRequestException("لا يمكن للفريق أن يواجه نفسه.");

            // ── Match generation via single engine ──
            bool isHomeAway = tournament.GetEffectiveMode() == TournamentMode.KnockoutHomeAway
                              || tournament.GetEffectiveMode() == TournamentMode.GroupsKnockoutHomeAway;

            string defaultStageName = request.Pairings.Count == 2 ? "Semi-final" : "Knockout";

            var pairingTuples = request.Pairings.Select(p => (
                p.HomeTeamId,
                p.AwayTeamId,
                RoundNumber: request.RoundNumber,
                StageName: string.IsNullOrWhiteSpace(p.StageName) ? defaultStageName : p.StageName));

            var matches = MatchGenerator.GenerateManualKnockout(
                tournament.Id,
                pairingTuples,
                isHomeAway,
                DateTime.UtcNow.AddDays(2));

            await _matchRepository.AddRangeAsync(matches, cancellationToken);

            var dtos = _mapper.Map<IEnumerable<MatchDto>>(matches);
            await _notifier.SendMatchesGeneratedAsync(dtos, cancellationToken);

            return new MatchListResponse(dtos.ToList());
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}
