using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Objections;

namespace Application.Interfaces;

public interface IObjectionService
{
    Task<IEnumerable<ObjectionDto>> GetAllAsync();
    Task<ObjectionDto?> GetByIdAsync(Guid id);
    Task<ObjectionDto> SubmitAsync(SubmitObjectionRequest request, Guid teamId);
    Task<ObjectionDto> ResolveAsync(Guid id, ResolveObjectionRequest request);
}
