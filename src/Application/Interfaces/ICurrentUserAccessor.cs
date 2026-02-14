using Domain.Entities;

namespace Application.Interfaces;

public interface ICurrentUserAccessor
{
    User? User { get; }
    void SetUser(User user);
    Guid? UserId { get; }
}
