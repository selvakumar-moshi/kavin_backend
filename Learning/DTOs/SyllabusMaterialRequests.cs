namespace LearningBackendAPI.DTOs
{
    // POST /api/SyllabusMaterial and PUT /api/SyllabusMaterial/{id} (form data)
    public class SyllabusMaterialRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        // Ids from GET /api/Folder. SubFolderId is required when the folder has sub folders, and must be the deepest level
        public string? FolderId { get; set; }
        public string? SubFolderId { get; set; }
        // The PDF. Required on create; on update a new file replaces the old one
        public IFormFile? File { get; set; }
    }

    // POST /api/SyllabusMaterial/search (also GET with the same query-string fields)
    public class SyllabusMaterialSearchRequest
    {
        public string? SearchTerm { get; set; }
        public string? FolderId { get; set; }
        // A sub folder also matches materials filed in any sub folder below it
        public string? SubFolderId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
