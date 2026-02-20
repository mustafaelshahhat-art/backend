using Application.Common.Models;
using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetPendingPayments;

public record GetPendingPaymentsQuery(int Page, int PageSize, Guid? CreatorId = null) : IRequest<PagedResult<PendingPaymentResponse>>;
