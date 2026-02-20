using Application.Common.Models;
using Application.Contracts.Admin.Responses;
using MediatR;

namespace Application.Features.Admin.Queries.GetDeadLetters;

public record GetDeadLettersQuery(int Page, int PageSize) : IRequest<PagedResult<DeadLetterMessageDto>>;
