using MediatR;
using Application.Interfaces;
using Application.DTOs.Users;
namespace Application.Features.Users.Commands.UploadAvatar;

public class UploadAvatarCommandHandler : IRequestHandler<UploadAvatarCommand, string>
{
    private readonly IUserService _userService;

    public UploadAvatarCommandHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<string> Handle(UploadAvatarCommand request, CancellationToken cancellationToken)
    {
        var uploadRequest = new UploadAvatarRequest
        {
            Base64Image = request.Base64Image,
            FileName = request.FileName
        };

        return await _userService.UploadAvatarAsync(request.UserId, uploadRequest, cancellationToken);
    }
}
