using FluentValidation;

namespace Application.Features.Tournaments.Commands.WithdrawTeam;

public class WithdrawTeamCommandValidator : AbstractValidator<WithdrawTeamCommand>
{
    public WithdrawTeamCommandValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
