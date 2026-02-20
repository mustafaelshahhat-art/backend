using Application.DTOs.Users;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Users.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserDto>
{
    private readonly IRepository<User> _userRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _realTimeNotifier;

    public UpdateUserCommandHandler(
        IRepository<User> userRepository,
        IMapper mapper,
        IRealTimeNotifier realTimeNotifier)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
    }

    public async Task<UserDto> Handle(UpdateUserCommand command, CancellationToken ct)
    {
        var id = command.Id;
        var request = command.Request;

        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user == null) throw new NotFoundException(nameof(User), id);

        if (!string.IsNullOrEmpty(request.Name)) user.Name = request.Name!;
        if (!string.IsNullOrEmpty(request.Phone)) user.Phone = request.Phone;
        if (request.GovernorateId.HasValue) user.GovernorateId = request.GovernorateId;
        if (request.CityId.HasValue) user.CityId = request.CityId;
        if (request.AreaId.HasValue) user.AreaId = request.AreaId;
        if (request.Age.HasValue) user.Age = request.Age;

        await _userRepository.UpdateAsync(user, ct);

        var dto = _mapper.Map<UserDto>(user);
        await _realTimeNotifier.SendUserUpdatedAsync(dto, ct);

        return dto;
    }
}
