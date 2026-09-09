using LearningBackendAPI.DTOs;

namespace LearningBackendAPI.Utils
{
    public static class PagingHelper
    {
        /// <summary>
        /// Normalizes page number/size (any value &lt;= 0, e.g. an explicit 0/0 payload, falls back
        /// to page 1 of 10) and slices an already-filtered in-memory list into a PagedResult.
        /// </summary>
        public static PagedResult<T> ToPagedResult<T>(List<T> filteredItems, int pageNumber, int pageSize)
        {
            var normalizedPageNumber = pageNumber > 0 ? pageNumber : 1;
            var normalizedPageSize = pageSize > 0 ? pageSize : 10;

            var pageItems = filteredItems
                .Skip((normalizedPageNumber - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList();

            return new PagedResult<T>
            {
                Items = pageItems,
                PageNumber = normalizedPageNumber,
                PageSize = normalizedPageSize,
                TotalCount = filteredItems.Count
            };
        }
    }
}
