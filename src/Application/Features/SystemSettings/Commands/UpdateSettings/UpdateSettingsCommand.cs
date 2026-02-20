using Application.DTOs.Settings;
using MediatR;

namespace Application.Features.SystemSettings.Commands.UpdateSettings;

public record UpdateSettingsCommand(SystemSettingsDto Settings, Guid AdminId) : IRequest<SystemSettingsDto>;
