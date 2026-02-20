namespace Application.Interfaces;

[Obsolete("Use PagedResult<SearchResultItem> instead.")]
public class SearchResponse
{
    public List<SearchResultItem> Results { get; set; } = new();
    public int TotalCount { get; set; }
}

public class SearchResultItem
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
}
