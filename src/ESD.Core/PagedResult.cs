namespace ESD.Core;

/// <summary>
/// Generic paged result used by read-side queries and API projections.
/// Reusable across every layer (Data → API → UI).
/// </summary>
public sealed record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int              TotalCount { get; init; }
    public int              Page { get; init; }
    public int              PageSize { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPrevPage => Page > 1;

    public static PagedResult<T> Of(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
        => new() { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
}
