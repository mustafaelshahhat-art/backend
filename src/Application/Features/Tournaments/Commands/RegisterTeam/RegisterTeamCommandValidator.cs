using FluentValidation;
using Application.Interfaces;
using Domain.Enums;

namespace Application.Features.Tournaments.Commands.RegisterTeam;

public class RegisterTeamCommandValidator : AbstractValidator<RegisterTeamCommand>
{
    public RegisterTeamCommandValidator(IUserService userService)
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        
        RuleFor(x => x.UserId)
            .CustomAsync(async (userId, context, cancellationToken) => {
                var user = await userService.GetByIdAsync(userId, cancellationToken);
                if (user?.Status != UserStatus.Active.ToString())
                {
                    context.AddFailure("UserId", "يجب تفعيل حسابك أولاً لتتمكن من التسجيل في البطولات.");
                }
            });
    }
}
