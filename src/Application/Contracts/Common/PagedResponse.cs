namespace Application.Contracts.Common;

/// <summary>
/// Standardized paged response envelope.
/// All paged endpoints return this shape:
/// { "items": [], "pageNumber": 1, "pageSize": 10, "totalCount": 0, "totalPages": 1 }
/// 
/// NOTE: Uses PageNumber (not Page) to maintain frontend compatibility.
/// </summary>
public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    public PagedResponse() { }

    public PagedResponse(List<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>
    /// Convert from legacy PagedResult to standardized PagedResponse.
    /// </summary>
    public static PagedResponse<T> FromPagedResult(Application.Common.Models.PagedResult<T> result)
        => new(result.Items, result.TotalCount, result.PageNumber, result.PageSize);
}
