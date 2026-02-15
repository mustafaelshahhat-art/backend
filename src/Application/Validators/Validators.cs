using FluentValidation;
using Application.DTOs.Auth;
using Application.DTOs.Teams;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using Application.DTOs.Users;

namespace Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
            .EmailAddress().WithMessage("البريد الإلكتروني غير صحيح");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة")
            .MinimumLength(8).WithMessage("كلمة المرور يجب أن تكون 8 أحرف على الأقل")
            .Matches("[A-Z]").WithMessage("كلمة المرور يجب أن تحتوي على حرف كبير واحد على الأقل")
            .Matches("[a-z]").WithMessage("كلمة المرور يجب أن تحتوي على حرف صغير واحد على الأقل")
            .Matches("[0-9]").WithMessage("كلمة المرور يجب أن تحتوي على رقم واحد على الأقل");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("الاسم مطلوب")
            .MinimumLength(3).WithMessage("الاسم يجب أن يكون 3 أحرف على الأقل");

        RuleFor(x => x.Phone)
            .Matches(@"^(010|011|012|015)\d{8}$")
            .WithMessage("رقم الهاتف يجب أن يكون رقم مصري صحيح (11 رقم)")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.NationalId)
            .Length(14).WithMessage("الرقم القومي يجب أن يتكون من 14 رقم")
            .Matches(@"^\d+$").WithMessage("الرقم القومي يجب أن يحتوي على أرقام فقط")
            .When(x => !string.IsNullOrEmpty(x.NationalId));

        RuleFor(x => x.Age)
            .InclusiveBetween(10, 100).WithMessage("السن يجب أن يكون بين 10 و 100 سنة")
            .When(x => x.Age.HasValue);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
            .EmailAddress().WithMessage("البريد الإلكتروني غير صحيح");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة");
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
        RuleFor(x => x.StartDate).GreaterThanOrEqualTo(DateTime.Today.AddDays(-1)).WithMessage("تاريخ بداية البطولة لا يمكن أن يكون في الماضي.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("تاريخ النهاية يجب أن يكون في نفس يوم بداية البطولة أو بعده.");
        RuleFor(x => x.RegistrationDeadline).LessThanOrEqualTo(x => x.StartDate).WithMessage("آخر موعد للتسجيل يجب أن يكون قبل أو يوم بداية البطولة.");
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



public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name != null);
        RuleFor(x => x.Age).InclusiveBetween(10, 100).When(x => x.Age.HasValue);
    }
}
