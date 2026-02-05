using FluentValidation;
using Application.DTOs.Auth;
using Application.DTOs.Teams;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using Application.DTOs.Objections;
using Application.DTOs.Users;

namespace Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Name).NotEmpty();
    }
}

public class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50); // Validating constraints from contract if any
        RuleFor(x => x.Founded).NotEmpty();
    }
}

public class CreateTournamentRequestValidator : AbstractValidator<CreateTournamentRequest>
{
    public CreateTournamentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.StartDate).GreaterThan(DateTime.UtcNow);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate);
        RuleFor(x => x.EntryFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxTeams).GreaterThan(1);
    }
}

public class AddMatchEventRequestValidator : AbstractValidator<AddMatchEventRequest>
{
    public AddMatchEventRequestValidator()
    {
        RuleFor(x => x.Type).NotEmpty().IsEnumName(typeof(Domain.Enums.MatchEventType));
        RuleFor(x => x.Minute).InclusiveBetween(0, 120);
        RuleFor(x => x.TeamId).NotEmpty();
    }
}

public class SubmitObjectionRequestValidator : AbstractValidator<SubmitObjectionRequest>
{
    public SubmitObjectionRequestValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();
        RuleFor(x => x.Type).NotEmpty().IsEnumName(typeof(Domain.Enums.ObjectionType));
        RuleFor(x => x.Description).NotEmpty().MinimumLength(10);
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name != null);
        RuleFor(x => x.Age).InclusiveBetween(10, 100).When(x => x.Age.HasValue);
    }
}
