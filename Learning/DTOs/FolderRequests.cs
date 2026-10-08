namespace LearningBackendAPI.DTOs
{
    public class FolderRequest
    {
        public string? Name { get; set; }
    }

    public class SubFolderRequest
    {
        public string? Name { get; set; }
        // Required on create; the folder of an existing sub folder can't be changed
        public string? FolderId { get; set; }
    }

    public class SubFolderResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FolderId { get; set; } = string.Empty;
    }

    public class FolderResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<SubFolderResponse> SubFolders { get; set; } = new();
    }

    // A quiz's validated folder / sub folder choice
    public class FolderSelection
    {
        public string? FolderId { get; set; }
        public string? FolderName { get; set; }
        public string? SubFolderId { get; set; }
        public string? SubFolderName { get; set; }
    }
}
