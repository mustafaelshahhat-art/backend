using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Matches;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

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
    [OutputCache(PolicyName = "ShortCache")]
    public async Task<ActionResult<IEnumerable<MatchMessageDto>>> GetChatHistory(Guid id, [FromQuery] int pageSize = 50, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;
        var messages = await _messageRepository.GetByMatchIdAsync(id, pageSize, page, cancellationToken);
        var dtos = messages.Select(m => new MatchMessageDto
        {
            Id = m.Id,
            MatchId = m.MatchId,
            SenderId = m.SenderId,
            SenderName = m.SenderName,
            Role = m.Role,
            Content = m.Content,
            Timestamp = m.Timestamp
        });
        return Ok(dtos);
    }
}
