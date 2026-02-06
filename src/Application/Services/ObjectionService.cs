using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Objections;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Shared.Exceptions;

namespace Application.Services;

public class ObjectionService : IObjectionService
{
    private readonly IRepository<Objection> _objectionRepository;
    private readonly IMapper _mapper;

    public ObjectionService(IRepository<Objection> objectionRepository, IMapper mapper)
    {
        _objectionRepository = objectionRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ObjectionDto>> GetAllAsync()
    {
        var objections = await _objectionRepository.FindAsync(_ => true, new[] { "Team.Captain", "Match.Tournament" });
        return _mapper.Map<IEnumerable<ObjectionDto>>(objections);
    }

    public async Task<IEnumerable<ObjectionDto>> GetByTeamIdAsync(Guid teamId)
    {
        var objections = await _objectionRepository.FindAsync(o => o.TeamId == teamId, new[] { "Team.Captain", "Match.Tournament" });
        return _mapper.Map<IEnumerable<ObjectionDto>>(objections);
    }

    public async Task<ObjectionDto?> GetByIdAsync(Guid id)
    {
        var objections = await _objectionRepository.FindAsync(o => o.Id == id, new[] { "Team.Captain", "Match.Tournament" });
        var objection = objections.FirstOrDefault();
        return objection == null ? null : _mapper.Map<ObjectionDto>(objection);
    }

    public async Task<ObjectionDto> SubmitAsync(SubmitObjectionRequest request, Guid teamId)
    {
        if (!Enum.TryParse<ObjectionType>(request.Type, true, out var type))
        {
            // Try matching normalized uppercase strings if PascalCase fails
            var normalizedTypes = Enum.GetNames<ObjectionType>().ToDictionary(t => t.ToUpperInvariant(), t => Enum.Parse<ObjectionType>(t));
            if (normalizedTypes.TryGetValue(request.Type.ToUpperInvariant().Replace("_", ""), out var fallbackType))
            {
                type = fallbackType;
            }
            else
            {
                throw new BadRequestException($"نوع الاعتراض غير صالح: {request.Type}");
            }
        }

        var objection = new Objection
        {
            MatchId = request.MatchId,
            TeamId = teamId,
            Type = type,
            Description = request.Description,
            Status = ObjectionStatus.Pending
        };

        await _objectionRepository.AddAsync(objection);
        return _mapper.Map<ObjectionDto>(objection);
    }

    public async Task<ObjectionDto> ResolveAsync(Guid id, ResolveObjectionRequest request)
    {
        var objection = await _objectionRepository.GetByIdAsync(id);
        if (objection == null) throw new NotFoundException(nameof(Objection), id);

        objection.Status = request.Approved ? ObjectionStatus.Approved : ObjectionStatus.Rejected;
        if (!string.IsNullOrEmpty(request.Notes))
        {
            objection.AdminNotes = request.Notes;
        }

        await _objectionRepository.UpdateAsync(objection);
        return _mapper.Map<ObjectionDto>(objection);
    }
}
