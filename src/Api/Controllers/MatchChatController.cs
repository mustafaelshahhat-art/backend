using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/matches")] // extending matches route
public class MatchChatController : ControllerBase
{
    private readonly IMatchMessageRepository _messageRepository;

    public MatchChatController(IMatchMessageRepository messageRepository)
    {
        _messageRepository = messageRepository;
    }

    [HttpGet("{id}/chat")]
    [Authorize]
    [ResponseCache(Duration = 5)]
    public async Task<ActionResult<IEnumerable<MatchMessage>>> GetChatHistory(Guid id, [FromQuery] int pageSize = 50, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (pageSize > 200) pageSize = 200;
        var messages = await _messageRepository.GetByMatchIdAsync(id, pageSize, page, cancellationToken);
        return Ok(messages);
    }
}
