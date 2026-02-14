using Application.Features.Admin.Commands.ClearDeadLetterMessages;
using Application.Features.Admin.Commands.RetryOutboxMessage;
using Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/admin/outbox")]
[Authorize(Policy = "RequireAdmin")]
public class OutboxAdminController : ControllerBase
{
    private readonly IOutboxAdminService _outboxAdminService;
    private readonly IMediator _mediator;

    public OutboxAdminController(IOutboxAdminService outboxAdminService, IMediator mediator)
    {
        _outboxAdminService = outboxAdminService;
        _mediator = mediator;
    }

    [HttpGet("dead-letters")]
    public async Task<IActionResult> GetDeadLetters([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _outboxAdminService.GetDeadLetterMessagesAsync(page, pageSize, ct);
        return Ok(new { messages = result.Messages, totalCount = result.TotalCount });
    }

    [HttpPost("dead-letters/{id}/retry")]
    public async Task<IActionResult> RetryDeadLetter(Guid id, CancellationToken ct = default)
    {
        var success = await _mediator.Send(new RetryOutboxMessageCommand(id), ct);
        if (!success) return NotFound("Dead letter message not found or not in DeadLetter status.");
        return Ok(new { message = "Message scheduled for retry." });
    }

    [HttpDelete("dead-letters")]
    public async Task<IActionResult> ClearDeadLetters(CancellationToken ct = default)
    {
        var count = await _mediator.Send(new ClearDeadLetterMessagesCommand(), ct);
        return Ok(new { count, message = $"{count} dead letters cleared." });
    }
}
