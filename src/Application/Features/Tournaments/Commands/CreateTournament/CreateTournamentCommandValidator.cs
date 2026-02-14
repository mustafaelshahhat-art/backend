using FluentValidation;
using Application.Validators;

namespace Application.Features.Tournaments.Commands.CreateTournament;

public class CreateTournamentCommandValidator : AbstractValidator<CreateTournamentCommand>
{
    public CreateTournamentCommandValidator()
    {
        RuleFor(x => x.Request)
            .SetValidator(new CreateTournamentRequestValidator());
    }
}
