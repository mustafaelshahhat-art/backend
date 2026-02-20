using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Auth.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IRepository<User> _userRepository;

    public LogoutCommandHandler(IRepository<User> userRepository) => _userRepository = userRepository;

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (user != null)
        {
            user.RefreshToken = null;
            user.TokenVersion++;
            await _userRepository.UpdateAsync(user, ct);
        }
        return Unit.Value;
    }
}
