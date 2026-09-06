namespace Tugu.Contracts.Common;

/// <summary>Envoltura estándar de listados paginados (va dentro de ApiResponse.Data).</summary>
public class PagedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }

    public required int TotalPages { get; init; }
}
