using Application.Common.Models;
using Application.DTOs.Matches;
using Application.Features.Matches.Commands.AddMatchEvent;
using Application.Features.Matches.Commands.EndMatch;
using Application.Features.Matches.Commands.StartMatch;
using Application.Features.Matches.Queries.GetMatchById;
using Application.Features.Matches.Queries.GetMatchesByTournament;
using Application.Features.Matches.Queries.GetMatchesPaged;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using FluentAssertions;
using MockQueryable;
using Moq;
using Match = Domain.Entities.Match;
using Shared.Exceptions;
using Xunit;

namespace UnitTests.Handlers.Matches;

/// <summary>
/// Phase 3 gate: Match handler tests.
/// Match command handlers already use repositories directly.
/// Query handlers were inlined during Phase 3 MatchService decommission.
/// </summary>
public class MatchHandlerTests
{
    private readonly Mock<IRepository<Match>> _matchRepo = new();
    private readonly Mock<IRepository<MatchEvent>> _eventRepo = new();
    private readonly Mock<IRepository<Team>> _teamRepo = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IRealTimeNotifier> _realTimeNotifier = new();
    private readonly Mock<IMatchEventNotifier> _matchEventNotifier = new();
    private readonly Mock<ITournamentLifecycleService> _lifecycleService = new();

    #region GetMatchById

    [Fact]
    public async Task GetMatchById_ExistingMatch_ReturnsProjectedDto()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var tournament = new Tournament { Id = tournamentId, Name = "Test Cup", CreatorUserId = Guid.NewGuid() };
        var homeTeam = new Team { Id = homeTeamId, Name = "Home FC" };
        var awayTeam = new Team { Id = awayTeamId, Name = "Away FC" };
        var match = new Match
        {
            Id = matchId,
            TournamentId = tournamentId,
            Tournament = tournament,
            HomeTeamId = homeTeamId,
            HomeTeam = homeTeam,
            AwayTeamId = awayTeamId,
            AwayTeam = awayTeam,
            Status = MatchStatus.Scheduled,
            Events = new List<MatchEvent>
            {
                new MatchEvent { Id = Guid.NewGuid(), MatchId = matchId, Type = MatchEventType.Goal, TeamId = homeTeamId, Minute = 10 }
            }
        };

        var mockQueryable = new List<Match> { match }.AsQueryable().BuildMock();
        _matchRepo.Setup(r => r.GetQueryable()).Returns(mockQueryable);
        _matchRepo.Setup(r => r.ExecuteFirstOrDefaultAsync(It.IsAny<IQueryable<MatchDto>>(), It.IsAny<CancellationToken>()))
            .Returns((IQueryable<MatchDto> q, CancellationToken _) => Task.FromResult(q.FirstOrDefault()));

        var handler = new GetMatchByIdQueryHandler(_matchRepo.Object);

        // Act
        var result = await handler.Handle(new GetMatchByIdQuery(matchId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(matchId);
        result.HomeTeamName.Should().Be("Home FC");
        result.AwayTeamName.Should().Be("Away FC");
        result.TournamentName.Should().Be("Test Cup");
        result.Status.Should().Be("Scheduled");
        result.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMatchById_NonExistent_ReturnsNull()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var mockQueryable = new List<Match>().AsQueryable().BuildMock();
        _matchRepo.Setup(r => r.GetQueryable()).Returns(mockQueryable);
        _matchRepo.Setup(r => r.ExecuteFirstOrDefaultAsync(It.IsAny<IQueryable<MatchDto>>(), It.IsAny<CancellationToken>()))
            .Returns((IQueryable<MatchDto> q, CancellationToken _) => Task.FromResult(q.FirstOrDefault()));

        var handler = new GetMatchByIdQueryHandler(_matchRepo.Object);

        // Act
        var result = await handler.Handle(new GetMatchByIdQuery(matchId), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMatchById_IncludesEventsWithPlayerNames()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var player = new Player { Id = playerId, Name = "Ahmed" };
        var tournament = new Tournament { Id = Guid.NewGuid(), Name = "Cup" };
        var homeTeam = new Team { Id = teamId, Name = "Team A" };
        var awayTeam = new Team { Id = Guid.NewGuid(), Name = "Team B" };
        var match = new Match
        {
            Id = matchId,
            TournamentId = tournament.Id,
            Tournament = tournament,
            HomeTeamId = teamId,
            HomeTeam = homeTeam,
            AwayTeamId = awayTeam.Id,
            AwayTeam = awayTeam,
            Status = MatchStatus.Live,
            Events = new List<MatchEvent>
            {
                new MatchEvent { Id = Guid.NewGuid(), MatchId = matchId, Type = MatchEventType.Goal, TeamId = teamId, PlayerId = playerId, Player = player, Minute = 25 }
            }
        };

        var mockQueryable = new List<Match> { match }.AsQueryable().BuildMock();
        _matchRepo.Setup(r => r.GetQueryable()).Returns(mockQueryable);
        _matchRepo.Setup(r => r.ExecuteFirstOrDefaultAsync(It.IsAny<IQueryable<MatchDto>>(), It.IsAny<CancellationToken>()))
            .Returns((IQueryable<MatchDto> q, CancellationToken _) => Task.FromResult(q.FirstOrDefault()));

        var handler = new GetMatchByIdQueryHandler(_matchRepo.Object);

        // Act
        var result = await handler.Handle(new GetMatchByIdQuery(matchId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Events.Should().HaveCount(1);
        result.Events[0].PlayerName.Should().Be("Ahmed");
        result.Events[0].Type.Should().Be("Goal");
        result.Events[0].Minute.Should().Be(25);
    }

    #endregion

    #region StartMatch

    [Fact]
    public async Task StartMatch_ValidMatch_SetsStatusToLive()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            TournamentId = tournamentId,
            Status = MatchStatus.Scheduled,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            HomeScore = 0,
            AwayScore = 0
        };

        _matchRepo.Setup(r => r.GetByIdAsync(matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);
        _matchRepo.Setup(r => r.UpdateAsync(It.IsAny<Match>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mapper.Setup(m => m.Map<MatchDto>(It.IsAny<Match>()))
            .Returns(new MatchDto { Id = matchId, Status = "Live" });

        var handler = new StartMatchCommandHandler(
            _matchRepo.Object, _teamRepo.Object, _mapper.Object,
            _matchEventNotifier.Object);

        var command = new StartMatchCommand(matchId, userId, UserRole.Admin.ToString());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        match.Status.Should().Be(MatchStatus.Live);
        _matchRepo.Verify(r => r.UpdateAsync(match, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _matchEventNotifier.Verify(n => n.SendMatchUpdateAsync(It.IsAny<Match>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region EndMatch

    [Fact]
    public async Task EndMatch_LiveMatch_SetsStatusToFinished()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            TournamentId = tournamentId,
            Status = MatchStatus.Live,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            HomeScore = 2,
            AwayScore = 1
        };

        _matchRepo.Setup(r => r.GetByIdAsync(matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);
        _matchRepo.Setup(r => r.UpdateAsync(It.IsAny<Match>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _lifecycleService.Setup(s => s.CheckAndFinalizeTournamentAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new global::Application.DTOs.Tournaments.TournamentLifecycleResult());
        _mapper.Setup(m => m.Map<MatchDto>(It.IsAny<Match>()))
            .Returns(new MatchDto { Id = matchId, Status = "Finished" });

        var handler = new EndMatchCommandHandler(
            _matchRepo.Object, _teamRepo.Object, _lifecycleService.Object,
            _mapper.Object, _matchEventNotifier.Object);

        var command = new EndMatchCommand(matchId, userId, UserRole.Admin.ToString());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        match.Status.Should().Be(MatchStatus.Finished);
        _matchRepo.Verify(r => r.UpdateAsync(match, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task EndMatch_TriggersLifecycleCheck()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            TournamentId = tournamentId,
            Status = MatchStatus.Live,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            HomeScore = 1,
            AwayScore = 1
        };

        _matchRepo.Setup(r => r.GetByIdAsync(matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);
        _matchRepo.Setup(r => r.UpdateAsync(It.IsAny<Match>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _lifecycleService.Setup(s => s.CheckAndFinalizeTournamentAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new global::Application.DTOs.Tournaments.TournamentLifecycleResult { TournamentId = tournamentId });
        _mapper.Setup(m => m.Map<MatchDto>(It.IsAny<Match>()))
            .Returns(new MatchDto { Id = matchId, Status = "Finished" });

        var handler = new EndMatchCommandHandler(
            _matchRepo.Object, _teamRepo.Object, _lifecycleService.Object,
            _mapper.Object, _matchEventNotifier.Object);

        var command = new EndMatchCommand(matchId, userId, UserRole.Admin.ToString());

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _lifecycleService.Verify(
            s => s.CheckAndFinalizeTournamentAsync(tournamentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region AddMatchEvent

    [Fact]
    public async Task AddMatchEvent_Goal_IncrementsScore()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            TournamentId = Guid.NewGuid(),
            Status = MatchStatus.Live,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            HomeScore = 0,
            AwayScore = 0,
            Events = new List<MatchEvent>()
        };

        _matchRepo.Setup(r => r.GetByIdAsync(matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);
        _matchRepo.Setup(r => r.GetByIdAsync(matchId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);
        _matchRepo.Setup(r => r.UpdateAsync(It.IsAny<Match>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _eventRepo.Setup(r => r.AddAsync(It.IsAny<MatchEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mapper.Setup(m => m.Map<MatchEventDto>(It.IsAny<MatchEvent>()))
            .Returns(new MatchEventDto { Type = "Goal" });
        _mapper.Setup(m => m.Map<MatchDto>(It.IsAny<Match>()))
            .Returns(new MatchDto { Id = matchId });

        var request = new AddMatchEventRequest
        {
            Type = "Goal",
            TeamId = homeTeamId,
            Minute = 35
        };

        var handler = new AddMatchEventCommandHandler(
            _matchRepo.Object, _eventRepo.Object, _realTimeNotifier.Object, _mapper.Object);

        var command = new AddMatchEventCommand(matchId, request, userId, UserRole.Admin.ToString());

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        match.HomeScore.Should().Be(1);
        match.AwayScore.Should().Be(0);
        _eventRepo.Verify(r => r.AddAsync(It.Is<MatchEvent>(e => e.Type == MatchEventType.Goal && e.TeamId == homeTeamId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMatchEvent_Card_DoesNotChangeScore()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            TournamentId = Guid.NewGuid(),
            Status = MatchStatus.Live,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            HomeScore = 2,
            AwayScore = 1,
            Events = new List<MatchEvent>()
        };

        _matchRepo.Setup(r => r.GetByIdAsync(matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);
        _matchRepo.Setup(r => r.GetByIdAsync(matchId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);
        _matchRepo.Setup(r => r.UpdateAsync(It.IsAny<Match>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _eventRepo.Setup(r => r.AddAsync(It.IsAny<MatchEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mapper.Setup(m => m.Map<MatchEventDto>(It.IsAny<MatchEvent>()))
            .Returns(new MatchEventDto { Type = "YellowCard" });
        _mapper.Setup(m => m.Map<MatchDto>(It.IsAny<Match>()))
            .Returns(new MatchDto { Id = matchId });

        var request = new AddMatchEventRequest
        {
            Type = "YellowCard",
            TeamId = homeTeamId,
            Minute = 50
        };

        var handler = new AddMatchEventCommandHandler(
            _matchRepo.Object, _eventRepo.Object, _realTimeNotifier.Object, _mapper.Object);

        var command = new AddMatchEventCommand(matchId, request, userId, UserRole.Admin.ToString());

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        match.HomeScore.Should().Be(2);
        match.AwayScore.Should().Be(1);
        _eventRepo.Verify(r => r.AddAsync(It.Is<MatchEvent>(e => e.Type == MatchEventType.YellowCard), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
