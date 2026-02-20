using Application.Common.Models;
using Application.Contracts.Admin.Responses;
using Application.Contracts.Common;
using Application.Features.Admin.Commands.ClearDeadLetterMessages;
using Application.Features.Admin.Commands.RetryOutboxMessage;
using Application.Features.Admin.Queries.GetDeadLetters;
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
    private readonly IMediator _mediator;

    public OutboxAdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dead-letters")]
    public async Task<ActionResult<PagedResult<DeadLetterMessageDto>>> GetDeadLetters([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDeadLettersQuery(page, pageSize), ct);
        return Ok(result);
    }

    [HttpPost("dead-letters/{id}/retry")]
    public async Task<ActionResult<MessageResponse>> RetryDeadLetter(Guid id, CancellationToken ct = default)
    {
        var success = await _mediator.Send(new RetryOutboxMessageCommand(id), ct);
        if (!success) return NotFound("Dead letter message not found or not in DeadLetter status.");
        return Ok(new MessageResponse("Message scheduled for retry."));
    }

    [HttpDelete("dead-letters")]
    public async Task<ActionResult<ClearDeadLettersResponse>> ClearDeadLetters(CancellationToken ct = default)
    {
        var count = await _mediator.Send(new ClearDeadLetterMessagesCommand(), ct);
        return Ok(new ClearDeadLettersResponse { Count = count, Message = $"{count} dead letters cleared." });
    }
}
