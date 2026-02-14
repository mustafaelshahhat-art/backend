using Application.Interfaces;
using Domain.Entities;

namespace Infrastructure.Authentication;

public class CurrentUserAccessor : ICurrentUserAccessor
{
    public User? User { get; private set; }

    public Guid? UserId => User?.Id;

    public void SetUser(User user)
    {
        User = user;
    }
}
