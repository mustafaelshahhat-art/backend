using AutoMapper;
using Domain.Entities;
using Application.DTOs.Users;
using Application.DTOs.Teams;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using Application.DTOs.Notifications;
using Application.DTOs.Analytics;
using Domain.Enums;

namespace Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User
        CreateMap<User, UserDto>()
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.GovernorateNameAr, o => o.MapFrom(s => s.GovernorateNav != null ? s.GovernorateNav.NameAr : null))
            .ForMember(d => d.CityNameAr, o => o.MapFrom(s => s.CityNav != null ? s.CityNav.NameAr : null))
            .ForMember(d => d.AreaNameAr, o => o.MapFrom(s => s.AreaNav != null ? s.AreaNav.NameAr : null))
            .ForMember(d => d.TeamName, o => o.Ignore())
            .ForMember(d => d.Activities, o => o.Ignore());

        CreateMap<User, UserPublicDto>()
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.GovernorateNameAr, o => o.MapFrom(s => s.GovernorateNav != null ? s.GovernorateNav.NameAr : null))
            .ForMember(d => d.CityNameAr, o => o.MapFrom(s => s.CityNav != null ? s.CityNav.NameAr : null))
            .ForMember(d => d.TeamName, o => o.Ignore());

        CreateMap<Activity, UserActivityDto>();

        // Tournament
        CreateMap<Tournament, TournamentDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Format, o => o.MapFrom(s => s.Format.ToString()))
            .ForMember(d => d.MatchType, o => o.MapFrom(s => s.MatchType.ToString()))
            .ForMember(d => d.Registrations, o => o.MapFrom(s => s.Registrations != null 
                ? s.Registrations.Where(r => r.Status != RegistrationStatus.Rejected) 
                : new List<TeamRegistration>()))
            .ForMember(d => d.WinnerTeamName, o => o.MapFrom(s => s.WinnerTeam != null ? s.WinnerTeam.Name : null))
            .ForMember(d => d.Mode, o => o.MapFrom(s => s.GetEffectiveMode()))
            .ForMember(d => d.AllowLateRegistration, o => o.MapFrom(s => s.AllowLateRegistration))
            .ForMember(d => d.LateRegistrationMode, o => o.MapFrom(s => s.LateRegistrationMode))
            .ForMember(d => d.OpeningMatchHomeTeamId, o => o.MapFrom(s => s.OpeningMatchHomeTeamId))
            .ForMember(d => d.OpeningMatchAwayTeamId, o => o.MapFrom(s => s.OpeningMatchAwayTeamId))
            .ForMember(d => d.AdminId, o => o.MapFrom(s => s.CreatorUserId))
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreatedAt))
            .ForMember(d => d.UpdatedAt, o => o.MapFrom(s => s.UpdatedAt));

        CreateMap<Team, TeamDto>()
            .ForMember(d => d.CaptainName, o => o.Ignore())
            .ForMember(d => d.PlayerCount, o => o.MapFrom(s => s.Players.Count))
            .ForMember(d => d.MaxPlayers, o => o.MapFrom(s => 10))
            .ForMember(d => d.Stats, o => o.MapFrom(s => s.Statistics));
        
        CreateMap<TeamStats, TeamStatsDto>()
            .ForMember(d => d.Matches, o => o.MapFrom(s => s.MatchesPlayed))
            .ForMember(d => d.Rank, o => o.Ignore());
        
        // Player
        CreateMap<Player, PlayerDto>();
        CreateMap<TeamRegistration, TeamRegistrationDto>()
            .ForMember(d => d.TeamName, o => o.MapFrom(s => s.Team != null ? s.Team.Name : string.Empty))
            .ForMember(d => d.CaptainName, o => o.Ignore())
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.RegisteredAt, o => o.MapFrom(s => s.CreatedAt));

        // Match
        CreateMap<Match, MatchDto>()
            .ForMember(d => d.HomeTeamName, o => o.MapFrom(s => s.HomeTeam != null ? s.HomeTeam.Name : string.Empty))
            .ForMember(d => d.AwayTeamName, o => o.MapFrom(s => s.AwayTeam != null ? s.AwayTeam.Name : string.Empty))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.TournamentName, o => o.MapFrom(s => s.Tournament != null ? s.Tournament.Name : null))
            .ForMember(d => d.TournamentCreatorId, o => o.MapFrom(s => s.Tournament != null ? s.Tournament.CreatorUserId : (Guid?)null))
            .ForMember(d => d.Events, o => o.MapFrom(s => s.Events));

        CreateMap<MatchEvent, MatchEventDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.PlayerName, o => o.MapFrom(s => s.Player != null ? s.Player.Name : string.Empty));



        // Notification
        CreateMap<Notification, NotificationDto>();
    }
}
