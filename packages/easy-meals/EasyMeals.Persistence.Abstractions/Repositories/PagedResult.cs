namespace EasyMeals.Persistence.Abstractions.Repositories;

/// <summary>
///     Represents a paged result set containing a subset of items and pagination metadata.
/// </summary>
/// <typeparam name="T">The type of items in the result set.</typeparam>
public sealed record PagedResult<T>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="PagedResult{T}" /> class.
	/// </summary>
	public PagedResult(IReadOnlyList<T> items, long totalCount, int page, int pageSize)
	{
		Items = items ?? [];
		TotalCount = totalCount;
		Page = page;
		PageSize = pageSize;
	}

	/// <summary>
	///     Gets the items in the current page.
	/// </summary>
	public IReadOnlyList<T> Items { get; init; }

	/// <summary>
	///     Gets the total number of items across all pages.
	/// </summary>
	public long TotalCount { get; init; }

	/// <summary>
	///     Gets the current page number (1-based).
	/// </summary>
	public int Page { get; init; }

	/// <summary>
	///     Gets the number of items per page.
	/// </summary>
	public int PageSize { get; init; }

	/// <summary>
	///     Gets the total number of pages.
	/// </summary>
	public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

	/// <summary>
	///     Gets whether there is a previous page.
	/// </summary>
	public bool HasPreviousPage => Page > 1;

	/// <summary>
	///     Gets whether there is a next page.
	/// </summary>
	public bool HasNextPage => Page < TotalPages;

	/// <summary>
	///     Gets the index of the first item on the current page (0-based).
	/// </summary>
	public int FirstItemIndex => (Page - 1) * PageSize;

	/// <summary>
	///     Gets the index of the last item on the current page (0-based).
	/// </summary>
	public int LastItemIndex => Math.Min(FirstItemIndex + PageSize - 1, (int)TotalCount - 1);

	/// <summary>
	///     Creates an empty paged result.
	/// </summary>
	public static PagedResult<T> Empty(int page = 1, int pageSize = 20)
		=> new([], 0, page, pageSize);

	/// <summary>
	///     Maps the items in the paged result to a different type.
	/// </summary>
	public PagedResult<TResult> Map<TResult>(Func<T, TResult> mapper)
	{
		List<TResult> mappedItems = Items.Select(mapper).ToList();
		return new PagedResult<TResult>(mappedItems, TotalCount, Page, PageSize);
	}
}