using Application.DTOs.Users;
using MediatR;

namespace Application.Features.Users.Commands.CreateTournamentCreator;

public record CreateTournamentCreatorCommand(CreateAdminRequest Request, Guid CreatedByAdminId) : IRequest<UserDto>;
