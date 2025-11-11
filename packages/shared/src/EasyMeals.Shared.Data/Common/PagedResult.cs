using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Common;

public record PagedResult<TDocument>(
    IEnumerable<TDocument>? Items,
    int TotalCount,
    int PageNumber,
    int PageSize) where TDocument : BaseDocument;