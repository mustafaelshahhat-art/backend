using Application.Common.Models;
using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetAllPaymentRequests;

public class GetAllPaymentRequestsQueryHandler : IRequestHandler<GetAllPaymentRequestsQuery, PagedResult<PendingPaymentResponse>>
{
    private readonly IRepository<TeamRegistration> _registrationRepository;

    public GetAllPaymentRequestsQueryHandler(IRepository<TeamRegistration> registrationRepository)
    {
        _registrationRepository = registrationRepository;
    }

    public async Task<PagedResult<PendingPaymentResponse>> Handle(GetAllPaymentRequestsQuery request, CancellationToken ct)
    {
        var page = request.Page;
        var pageSize = request.PageSize;
        var creatorId = request.CreatorId;

        if (pageSize > 100) pageSize = 100;

        var query = _registrationRepository.GetQueryable()
            .Where(r => (r.Status == RegistrationStatus.PendingPaymentReview ||
                  r.Status == RegistrationStatus.Approved ||
                  r.Status == RegistrationStatus.Rejected) &&
                 (!creatorId.HasValue || r.Tournament!.CreatorUserId == creatorId.Value));

        var totalCount = await _registrationRepository.ExecuteCountAsync(query, ct);

        var projected = await _registrationRepository.ExecuteQueryAsync(query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new PendingPaymentResponse
            {
                Registration = new TeamRegistrationDto
                {
                    Id = r.Id,
                    TeamId = r.TeamId,
                    TeamName = r.Team != null ? r.Team.Name : string.Empty,
                    CaptainName = r.Team != null ? r.Team.Players.Where(p => p.TeamRole == Domain.Enums.TeamRole.Captain).Select(p => p.Name).FirstOrDefault() ?? string.Empty : string.Empty,
                    Status = r.Status.ToString(),
                    PaymentReceiptUrl = r.PaymentReceiptUrl,
                    SenderNumber = r.SenderNumber,
                    RejectionReason = r.RejectionReason,
                    PaymentMethod = r.PaymentMethod,
                    RegisteredAt = r.CreatedAt,
                    TournamentId = r.TournamentId
                },
                Tournament = new TournamentDto
                {
                    Id = r.Tournament != null ? r.Tournament.Id : Guid.Empty,
                    Name = r.Tournament != null ? r.Tournament.Name : string.Empty,
                    EntryFee = r.Tournament != null ? r.Tournament.EntryFee : 0,
                    CreatorUserId = r.Tournament != null ? r.Tournament.CreatorUserId : null,
                    WalletNumber = r.Tournament != null ? r.Tournament.WalletNumber : null,
                    InstaPayNumber = r.Tournament != null ? r.Tournament.InstaPayNumber : null,
                    PaymentMethodsJson = r.Tournament != null ? r.Tournament.PaymentMethodsJson : null
                }
            }), ct);

        return new PagedResult<PendingPaymentResponse>(projected, totalCount, page, pageSize);
    }
}
