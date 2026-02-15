using MediatR;
using Application.Interfaces;
using Application.DTOs.Users;
using System;
using System.IO;

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
        if (string.IsNullOrWhiteSpace(request.Base64Image))
            throw new ArgumentException("Image data is required");

        var base64Data = request.Base64Image;
        if (base64Data.Contains(","))
        {
            base64Data = base64Data.Substring(base64Data.IndexOf(",") + 1);
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Data);
        }
        catch
        {
            throw new ArgumentException("Invalid image data");
        }

        using var stream = new MemoryStream(bytes);
        
        var ext = Path.GetExtension(request.FileName)?.ToLower() ?? ".jpg";
        var contentType = ext switch 
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };

        return await _userService.UploadAvatarAsync(request.UserId, stream, request.FileName, contentType, cancellationToken);
    }
}
