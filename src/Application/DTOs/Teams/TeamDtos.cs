using System;
using System.Collections.Generic;

namespace Application.DTOs.Teams;

public class TeamDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CaptainId { get; set; }
    public string CaptainName { get; set; } = string.Empty;
    public string? Logo { get; set; }
    public string Founded { get; set; } = string.Empty;
    public string? City { get; set; }
    public List<PlayerDto> Players { get; set; } = new();
}

public class CreateTeamRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Logo { get; set; }
    public string Founded { get; set; } = string.Empty;
    public string? City { get; set; }
}

public class UpdateTeamRequest
{
    public string? Name { get; set; }
    public string? Logo { get; set; }
    public string? City { get; set; }
}
