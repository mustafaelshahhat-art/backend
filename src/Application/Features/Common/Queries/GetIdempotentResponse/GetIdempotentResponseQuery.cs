using MediatR;
using Domain.Entities;
using Domain.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Common.Queries.GetIdempotentResponse;

public record GetIdempotentResponseQuery(string Key) : IRequest<IdempotentRequest?>;

public class GetIdempotentResponseQueryHandler : IRequestHandler<GetIdempotentResponseQuery, IdempotentRequest?>
{
    private readonly IRepository<IdempotentRequest> _repository;

    public GetIdempotentResponseQueryHandler(IRepository<IdempotentRequest> repository)
    {
        _repository = repository;
    }

    public async Task<IdempotentRequest?> Handle(GetIdempotentResponseQuery request, CancellationToken cancellationToken)
    {
        var results = await _repository.FindAsync(r => r.Key == request.Key, cancellationToken);
        return System.Linq.Enumerable.FirstOrDefault(results);
    }
}
