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
    [Authorize] // Check if user part of match? Skipping strict check for now as purely chat history
    public async Task<ActionResult<IEnumerable<MatchMessage>>> GetChatHistory(Guid id)
    {
        var messages = await _messageRepository.GetByMatchIdAsync(id);
        return Ok(messages);
    }
}
