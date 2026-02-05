using AutoMapper;
using Domain.Entities;
using Application.DTOs.Users;
using Application.DTOs.Teams;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using Application.DTOs.Objections;
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
            .ForMember(d => d.Role, o => o.MapFrom(s => s.TeamId.HasValue ? "Captain" : s.Role.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // Tournament
        CreateMap<Tournament, TournamentDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status))
            .ForMember(d => d.Registrations, o => o.MapFrom(s => s.Registrations != null 
                ? s.Registrations.Where(r => r.Status != RegistrationStatus.Rejected) 
                : new List<TeamRegistration>()));

        // Team
        CreateMap<Team, TeamDto>()
            .ForMember(d => d.CaptainName, o => o.MapFrom(s => s.Captain != null ? s.Captain.Name : string.Empty));
        
        // Player
        CreateMap<Player, PlayerDto>();
        CreateMap<TeamRegistration, TeamRegistrationDto>()
            .ForMember(d => d.TeamName, o => o.MapFrom(s => s.Team != null ? s.Team.Name : string.Empty))
            .ForMember(d => d.CaptainName, o => o.MapFrom(s => s.Team != null && s.Team.Captain != null ? s.Team.Captain.Name : string.Empty))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.RegisteredAt, o => o.MapFrom(s => s.CreatedAt));

        // Match
        CreateMap<Match, MatchDto>()
            .ForMember(d => d.HomeTeamName, o => o.MapFrom(s => s.HomeTeam != null ? s.HomeTeam.Name : string.Empty))
            .ForMember(d => d.AwayTeamName, o => o.MapFrom(s => s.AwayTeam != null ? s.AwayTeam.Name : string.Empty))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.RefereeName, o => o.MapFrom(s => s.Referee != null ? s.Referee.Name : string.Empty))
            .ForMember(d => d.Events, o => o.MapFrom(s => s.Events));

        CreateMap<MatchEvent, MatchEventDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.PlayerName, o => o.MapFrom(s => s.Player != null ? s.Player.Name : string.Empty));

        // Objection
        CreateMap<Objection, ObjectionDto>()
            .ForMember(d => d.TeamName, o => o.MapFrom(s => s.Team != null ? s.Team.Name : string.Empty))
            .ForMember(d => d.CaptainName, o => o.MapFrom(s => s.Team != null && s.Team.Captain != null ? s.Team.Captain.Name : string.Empty))
            .ForMember(d => d.TournamentName, o => o.MapFrom(s => s.Match != null && s.Match.Tournament != null ? s.Match.Tournament.Name : string.Empty))
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // Notification
        CreateMap<Notification, NotificationDto>();
    }
}
