using BuildingBlocks.ServiceDefaults.Errors;

namespace BuildingBlocks.ServiceDefaults.Paging;

public sealed record PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;

    public static PageRequest From(int? page, int? pageSize)
    {
        var p = page ?? 1;
        var size = pageSize ?? DefaultPageSize;
        if (p < 1)
        {
            throw new ValidationException("page must be greater than or equal to 1.");
        }

        if (size is < 1 or > MaxPageSize)
        {
            throw new ValidationException($"pageSize must be between 1 and {MaxPageSize}.");
        }

        return new PageRequest(p, size);
    }
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
