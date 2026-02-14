using FluentValidation;

namespace Application.Features.Users.Commands.UploadAvatar;

public class UploadAvatarCommandValidator : AbstractValidator<UploadAvatarCommand>
{
    public UploadAvatarCommandValidator()
    {
        RuleFor(x => x.Base64Image)
            .NotEmpty().WithMessage("Image data is required")
            .Must(BeValidBase64).WithMessage("Invalid image data")
            .Must(BeValidSize).WithMessage("File size must be less than 2MB");
    }

    private bool BeValidBase64(string base64Image)
    {
        if (string.IsNullOrWhiteSpace(base64Image)) return false;

        var data = base64Image;
        if (data.StartsWith("data:image"))
        {
            var commaIndex = data.IndexOf(",");
            if (commaIndex != -1)
            {
                data = data.Substring(commaIndex + 1);
            }
        }

        try
        {
            Convert.FromBase64String(data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool BeValidSize(string base64Image)
    {
        if (string.IsNullOrWhiteSpace(base64Image)) return true; // Handled by NotEmpty

        var data = base64Image;
        if (data.StartsWith("data:image"))
        {
            var commaIndex = data.IndexOf(",");
            if (commaIndex != -1)
            {
                data = data.Substring(commaIndex + 1);
            }
        }

        try
        {
            var bytes = Convert.FromBase64String(data);
            return bytes.Length <= 2 * 1024 * 1024;
        }
        catch
        {
            return false; // Handled by BeValidBase64
        }
    }
}
