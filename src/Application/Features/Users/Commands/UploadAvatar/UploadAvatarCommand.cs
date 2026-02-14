using MediatR;

namespace Application.Features.Users.Commands.UploadAvatar;

public record UploadAvatarCommand(Guid UserId, string Base64Image, string FileName) : IRequest<string>;
