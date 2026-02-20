using FluentValidation;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

namespace Application.Features.Tournaments.Commands.RegisterTeam;

public class RegisterTeamCommandValidator : AbstractValidator<RegisterTeamCommand>
{
    public RegisterTeamCommandValidator(IRepository<User> userRepository)
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        
        RuleFor(x => x.UserId)
            .CustomAsync(async (userId, context, cancellationToken) => {
                var user = await userRepository.GetByIdAsync(userId, cancellationToken);
                if (user?.Status != UserStatus.Active)
                {
                    context.AddFailure("UserId", "يجب تفعيل حسابك أولاً لتتمكن من التسجيل في البطولات.");
                }
            });
    }
}
