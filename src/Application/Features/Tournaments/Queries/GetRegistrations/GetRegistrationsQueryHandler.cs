using Application.Common.Models;
using Application.DTOs.Tournaments;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetRegistrations;

public class GetRegistrationsQueryHandler : IRequestHandler<GetRegistrationsQuery, PagedResult<TeamRegistrationDto>>
{
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IMapper _mapper;

    public GetRegistrationsQueryHandler(IRepository<TeamRegistration> registrationRepository, IMapper mapper)
    {
        _registrationRepository = registrationRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<TeamRegistrationDto>> Handle(GetRegistrationsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize > 100 ? 100 : request.PageSize;

        var (items, totalCount) = await _registrationRepository.GetPagedAsync(
            request.Page,
            pageSize,
            r => r.TournamentId == request.TournamentId,
            q => q.OrderByDescending(r => r.CreatedAt),
            cancellationToken,
            r => r.Team!,
            r => r.Team!.Players
        );

        var dtos = _mapper.Map<List<TeamRegistrationDto>>(items);
        return new PagedResult<TeamRegistrationDto>(dtos, totalCount, request.Page, pageSize);
    }
}
