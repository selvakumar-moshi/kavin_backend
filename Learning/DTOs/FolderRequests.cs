namespace LearningBackendAPI.DTOs
{
    // One node of a folder tree: a name plus any number of nested sub folders, to any depth
    public class SubFolderNodeRequest
    {
        public string? Name { get; set; }
        public List<SubFolderNodeRequest>? SubFolders { get; set; }
    }

    public class FolderRequest
    {
        public string? Name { get; set; }
        // A tree under this folder: created with it on create; merged in on update (existing names reused)
        public List<SubFolderNodeRequest>? SubFolders { get; set; }
    }

    public class SubFolderRequest
    {
        public string? Name { get; set; }
        // Required on create: the parent - a folder or another sub folder. A sub folder's parent can't be changed
        public string? FolderId { get; set; }
        // A branch under this sub folder: created with it on create; merged in on update (existing names reused)
        public List<SubFolderNodeRequest>? SubFolders { get; set; }
    }

    public class SubFolderResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        // The parent: a folder, or another sub folder
        public string FolderId { get; set; } = string.Empty;
        public List<SubFolderResponse> SubFolders { get; set; } = new();
    }

    public class FolderResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<SubFolderResponse> SubFolders { get; set; } = new();
    }

    // A quiz's validated folder / sub folder choice (SubFolder is the leaf the quiz is filed under)
    public class FolderSelection
    {
        public string? FolderId { get; set; }
        public string? FolderName { get; set; }
        public string? SubFolderId { get; set; }
        public string? SubFolderName { get; set; }
    }
}
