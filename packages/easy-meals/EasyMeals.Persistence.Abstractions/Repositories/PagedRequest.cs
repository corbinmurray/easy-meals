namespace EasyMeals.Persistence.Abstractions.Repositories;

/// <summary>
///     Represents a request for paged data.
/// </summary>
public record PagedRequest
{
	/// <summary>
	///     Creates a new paged request with default values (page 1, 20 items).
	/// </summary>
	public PagedRequest()
	{
	}

	/// <summary>
	///     Creates a new paged request.
	/// </summary>
	public PagedRequest(int page, int pageSize)
	{
		Page = page;
		PageSize = pageSize;
	}

	/// <summary>
	///     Gets the page number (1-based). Minimum value is 1.
	/// </summary>
	public int Page
	{
		get;
		init => field = value < 1 ? 1 : value;
	} = 1;

	/// <summary>
	///     Gets the page size. Must be between 1 and MaxPageSize.
	/// </summary>
	public int PageSize
	{
		get;
		init => field = value < 1 ? 1 : value > MaxPageSize ? MaxPageSize : value;
	} = 20;

	/// <summary>
	///     Gets the maximum allowed page size.
	/// </summary>
	public const int MaxPageSize = 100;

	/// <summary>
	///     Gets the number of items to skip.
	/// </summary>
	public int Skip => (Page - 1) * PageSize;

	/// <summary>
	///     Gets the number of items to take.
	/// </summary>
	public int Take => PageSize;


	/// <summary>
	///     Creates a request for the first page.
	/// </summary>
	public static PagedRequest FirstPage(int pageSize = 20) => new(1, pageSize);

	/// <summary>
	///     Creates a request for the next page.
	/// </summary>
	public PagedRequest NextPage() => this with { Page = Page + 1 };

	/// <summary>
	///     Creates a request for the previous page.
	/// </summary>
	public PagedRequest PreviousPage() => this with { Page = Math.Max(1, Page - 1) };
}

/// <summary>
///     Extended paged request with sorting and filtering support.
/// </summary>
public sealed record PagedRequest<TFilter> : PagedRequest
{
	public string? SortBy { get; init; }
	public SortDirection SortDirection { get; init; } = SortDirection.Ascending;
	public TFilter? Filter { get; init; }
}

public enum SortDirection
{
	Ascending,
	Descending
}