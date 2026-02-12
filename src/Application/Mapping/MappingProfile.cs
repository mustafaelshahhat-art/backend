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
            .ForMember(d => d.TeamName, o => o.Ignore())
            .ForMember(d => d.Activities, o => o.Ignore());

        CreateMap<User, UserPublicDto>()
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.TeamName, o => o.Ignore());

        CreateMap<Activity, UserActivityDto>();

        // Tournament
        CreateMap<Tournament, TournamentDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status))
            .ForMember(d => d.Format, o => o.MapFrom(s => s.Format.ToString()))
            .ForMember(d => d.MatchType, o => o.MapFrom(s => s.MatchType.ToString()))
            .ForMember(d => d.Registrations, o => o.MapFrom(s => s.Registrations != null 
                ? s.Registrations.Where(r => r.Status != RegistrationStatus.Rejected) 
                : new List<TeamRegistration>()))
            .ForMember(d => d.WinnerTeamName, o => o.MapFrom(s => s.WinnerTeam != null ? s.WinnerTeam.Name : null))
            .ForMember(d => d.Mode, o => o.MapFrom(s => s.GetEffectiveMode()));

        CreateMap<Team, TeamDto>()
            .ForMember(d => d.CaptainName, o => o.MapFrom(s => s.Players != null 
                ? (s.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain) != null ? s.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain).Name : string.Empty) 
                : string.Empty))
            .ForMember(d => d.PlayerCount, o => o.MapFrom(s => s.Players != null ? s.Players.Count : 0))
            .ForMember(d => d.MaxPlayers, o => o.MapFrom(s => 10));
        
        // Player
        CreateMap<Player, PlayerDto>();
        CreateMap<TeamRegistration, TeamRegistrationDto>()
            .ForMember(d => d.TeamName, o => o.MapFrom(s => s.Team != null ? s.Team.Name : string.Empty))
            .ForMember(d => d.TeamLogoUrl, o => o.MapFrom(s => s.Team != null ? s.Team.Logo : null))
            .ForMember(d => d.CaptainName, o => o.MapFrom(s => s.Team != null && s.Team.Players != null 
                ? (s.Team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain) != null ? s.Team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain).Name : string.Empty) 
                : string.Empty))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.RegisteredAt, o => o.MapFrom(s => s.CreatedAt));

        // Match
        CreateMap<Match, MatchDto>()
            .ForMember(d => d.HomeTeamName, o => o.MapFrom(s => s.HomeTeam != null ? s.HomeTeam.Name : string.Empty))
            .ForMember(d => d.HomeTeamLogoUrl, o => o.MapFrom(s => s.HomeTeam != null ? s.HomeTeam.Logo : null))
            .ForMember(d => d.AwayTeamName, o => o.MapFrom(s => s.AwayTeam != null ? s.AwayTeam.Name : string.Empty))
            .ForMember(d => d.AwayTeamLogoUrl, o => o.MapFrom(s => s.AwayTeam != null ? s.AwayTeam.Logo : null))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))

            .ForMember(d => d.Events, o => o.MapFrom(s => s.Events));

        CreateMap<MatchEvent, MatchEventDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.PlayerName, o => o.MapFrom(s => s.Player != null ? s.Player.Name : string.Empty));



        // Notification
        CreateMap<Notification, NotificationDto>();
    }
}
