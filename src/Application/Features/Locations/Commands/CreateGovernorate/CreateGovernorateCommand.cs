using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Commands.CreateGovernorate;

public record CreateGovernorateCommand(CreateGovernorateRequest Request) : IRequest<GovernorateAdminDto>;
