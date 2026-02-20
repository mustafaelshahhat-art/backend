using Application.DTOs.Matches;

namespace Application.DTOs;

public class MatchListResponse
{
    public List<MatchDto> Matches { get; set; } = new();
    public int Count { get; set; }

    public MatchListResponse(List<MatchDto> matches)
    {
        Matches = matches;
        Count = matches.Count;
    }
}
