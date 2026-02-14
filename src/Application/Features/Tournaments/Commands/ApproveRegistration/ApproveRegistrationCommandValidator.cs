using FluentValidation;

namespace Application.Features.Tournaments.Commands.ApproveRegistration;

public class ApproveRegistrationCommandValidator : AbstractValidator<ApproveRegistrationCommand>
{
    public ApproveRegistrationCommandValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.UserRole).NotEmpty();
    }
}
