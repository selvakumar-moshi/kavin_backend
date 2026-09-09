namespace LearningBackendAPI.Utils
{
    public static class TextSearchHelper
    {
        /// <summary>
        /// Applies an optional "search everything" term (OR across defaultSearchFields) combined
        /// with an optional per-field filter map (AND across provided fields) to an in-memory list.
        /// Matching is case-insensitive substring matching, mirroring the Mongo regex search used
        /// for the User search API.
        /// </summary>
        public static List<T> ApplyFilter<T>(
            List<T> items,
            string? searchTerm,
            Dictionary<string, string>? globalFilter,
            Dictionary<string, Func<T, string?>> fieldAccessors,
            IEnumerable<string> defaultSearchFields)
        {
            // Only trust globalFilter entries whose key is an actual recognized/searchable field.
            // ASP.NET Core's [FromQuery] Dictionary<string,string> binder can greedily absorb
            // unrelated sibling query parameters (e.g. searchTerm) as phantom dictionary entries
            // when no explicit globalFilter[key]=value is present in the query string - ignoring
            // unrecognized keys here protects against that instead of treating them as
            // impossible-to-satisfy filter conditions.
            var recognizedFilters = (globalFilter ?? new Dictionary<string, string>())
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value) && fieldAccessors.ContainsKey(kv.Key.ToLowerInvariant()))
                .ToList();

            var hasSearchTerm = !string.IsNullOrWhiteSpace(searchTerm);
            var hasGlobalFilter = recognizedFilters.Count > 0;

            if (!hasSearchTerm && !hasGlobalFilter)
            {
                return items;
            }

            return items.Where(item =>
            {
                var matchesSearchTerm = !hasSearchTerm || defaultSearchFields.Any(field =>
                    Matches(item, field, searchTerm!, fieldAccessors));

                var matchesGlobalFilter = !hasGlobalFilter || recognizedFilters.All(kv =>
                    Matches(item, kv.Key, kv.Value, fieldAccessors));

                return matchesSearchTerm && matchesGlobalFilter;
            }).ToList();
        }

        private static bool Matches<T>(T item, string field, string value, Dictionary<string, Func<T, string?>> fieldAccessors)
        {
            if (!fieldAccessors.TryGetValue(field.ToLowerInvariant(), out var accessor))
            {
                return false;
            }

            var fieldValue = accessor(item) ?? "";
            return fieldValue.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
