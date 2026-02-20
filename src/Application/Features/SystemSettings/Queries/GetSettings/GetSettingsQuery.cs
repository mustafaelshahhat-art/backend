using Application.DTOs.Settings;
using MediatR;

namespace Application.Features.SystemSettings.Queries.GetSettings;

public record GetSettingsQuery() : IRequest<SystemSettingsDto>;
