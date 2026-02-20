using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Commands.InvitePlayer;

public record InvitePlayerCommand(Guid TeamId, Guid CaptainId, AddPlayerRequest Request) : IRequest<JoinRequestDto>;
