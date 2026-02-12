using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Objections;

namespace Application.Interfaces;

public interface IObjectionService
{
    Task<IEnumerable<ObjectionDto>> GetAllAsync(Guid? creatorId = null);
    Task<IEnumerable<ObjectionDto>> GetByTeamIdAsync(Guid teamId);
    Task<ObjectionDto?> GetByIdAsync(Guid id, Guid userId, string userRole);
    Task<ObjectionDto> SubmitAsync(SubmitObjectionRequest request, Guid teamId);
    Task<ObjectionDto> ResolveAsync(Guid id, ResolveObjectionRequest request, Guid userId, string userRole);
}
