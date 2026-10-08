using System.Linq.Expressions;
using DenialsCommandCenter.Api.Configuration;

namespace DenialsCommandCenter.Api.Endpoints;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => (TotalItems + PageSize - 1) / PageSize;
}

// PageSize is left null when the request does not ask for one; the endpoint's LimitsOptions supply the default and the cap.
public sealed record GridQuery(string? Search = null, string? Sort = null, string? Direction = null, int Page = 1, int? PageSize = null)
{
    public bool Descending => Direction == "desc";

    public string? SearchPattern => string.IsNullOrWhiteSpace(Search)
        ? null
        : $"%{Search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";

    public int PageSizeOrDefault(LimitsOptions limits) => PageSize ?? limits.DefaultPageSize;

    public int Skip(LimitsOptions limits) => (Page - 1) * PageSizeOrDefault(limits);

    public string? Problem(IReadOnlyCollection<string> sortColumns, LimitsOptions limits)
    {
        var pageSize = PageSizeOrDefault(limits);
        if (Page < 1) return "Page must be 1 or more.";
        if (pageSize < 1 || pageSize > limits.MaxPageSize) return $"Page size must be between 1 and {limits.MaxPageSize}.";
        if ((long)(Page - 1) * pageSize > int.MaxValue) return "Page is out of range.";
        if (Direction is not (null or "" or "asc" or "desc")) return "Direction must be asc or desc.";
        if (!string.IsNullOrEmpty(Sort) && !sortColumns.Contains(Sort)) return $"Sort must be one of {string.Join(", ", sortColumns)}.";
        return null;
    }
}

public static class SortingExtensions
{
    public static IOrderedQueryable<T> SortBy<T, TKey>(this IQueryable<T> query, Expression<Func<T, TKey>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);
}
