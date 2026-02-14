using MediatR;
using Domain.Entities;
using Domain.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Common.Commands.CreateIdempotentRequest;

public record CreateIdempotentRequestCommand(string Key, string Route, string? ResponseBody) : IRequest;

public class CreateIdempotentRequestCommandHandler : IRequestHandler<CreateIdempotentRequestCommand>
{
    private readonly IRepository<IdempotentRequest> _repository;

    public CreateIdempotentRequestCommandHandler(IRepository<IdempotentRequest> repository)
    {
        _repository = repository;
    }

    public async Task Handle(CreateIdempotentRequestCommand request, CancellationToken cancellationToken)
    {
        var idempotentRequest = new IdempotentRequest
        {
            Key = request.Key,
            Route = request.Route,
            ResponseBody = request.ResponseBody,
            Status = IdempotencyStatus.Completed,
            StatusCode = 201
        };

        await _repository.AddAsync(idempotentRequest, cancellationToken);
    }
}
