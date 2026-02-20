using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Commands.AddGuestPlayer;

public record AddGuestPlayerCommand(Guid TeamId, Guid CaptainId, AddGuestPlayerRequest Request) : IRequest<PlayerDto>;
