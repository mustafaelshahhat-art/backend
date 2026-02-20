using MediatR;

namespace Application.Features.SystemSettings.Queries.GetMaintenanceStatus;

public record GetMaintenanceStatusQuery() : IRequest<bool>;
