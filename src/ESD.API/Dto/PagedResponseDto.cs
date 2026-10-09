namespace ESD.API.Dto;

/// <summary>Paged envelope returned by list endpoints.</summary>
public sealed record PagedResponseDto<T>(
    IReadOnlyList<T> Items,
    int              TotalCount,
    int              Page,
    int              PageSize,
    int              TotalPages,
    bool             HasNextPage,
    bool             HasPrevPage);
