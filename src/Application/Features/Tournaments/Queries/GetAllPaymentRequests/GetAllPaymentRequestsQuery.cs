using Application.Common.Models;
using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetAllPaymentRequests;

public record GetAllPaymentRequestsQuery(int Page, int PageSize, Guid? CreatorId = null) : IRequest<PagedResult<PendingPaymentResponse>>;
