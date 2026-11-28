namespace LearningBackendAPI.DTOs
{
    // One node of a folder tree: a name plus any number of nested sub folders, to any depth
    [System.Text.Json.Serialization.JsonConverter(typeof(SubFolderNodeRequestConverter))]
    public class SubFolderNodeRequest
    {
        public string? Name { get; set; }
        public List<SubFolderNodeRequest>? SubFolders { get; set; }
    }

    public abstract class FolderRequestBase
    {
        public string? Name { get; set; }
        // A tree under this node: created with it on create; merged in on update (existing names reused)
        public List<SubFolderNodeRequest>? SubFolders { get; set; }
        // Optional picture for this folder / sub folder (JPG, PNG or WEBP, max 5 MB). Only sent as form data (multipart);
        // on update a new picture replaces the old one
        [System.Text.Json.Serialization.JsonIgnore]
        public IFormFile? File { get; set; }
        // Update only: true removes the current picture (ignored when a new File is sent)
        public bool? RemoveImage { get; set; }
    }

    public class FolderRequest : FolderRequestBase
    {
    }

    public class SubFolderRequest : FolderRequestBase
    {
        // Required on create: the parent - a folder or another sub folder. A sub folder's parent can't be changed
        public string? FolderId { get; set; }
    }

    // Form data for POST / PUT /api/Folder (what Swagger shows). subFolders is a JSON array in a text field, e.g.
    // [{"name":"2023","subFolders":["Paper 1","Paper 2"]},"2024"]
    public class FolderFormRequest
    {
        public string? Name { get; set; }
        // JSON array of sub folders: each a name or { "name": "...", "subFolders": [...] }, nested to any depth
        public string? SubFolders { get; set; }
        // Optional picture (JPG, PNG or WEBP, max 5 MB); on update it replaces the old one
        public IFormFile? File { get; set; }
        // Update only: true removes the current picture (ignored when a File is sent)
        public bool? RemoveImage { get; set; }

        public FolderRequest ToRequest() => new()
        {
            Name = Name,
            SubFolders = FolderFormParser.ParseSubFolders(SubFolders),
            File = File,
            RemoveImage = RemoveImage
        };
    }

    // Form data for POST / PUT /api/SubFolder
    public class SubFolderFormRequest
    {
        // Create only: the parent - a folder or another sub folder
        public string? FolderId { get; set; }
        public string? Name { get; set; }
        // JSON array of sub folders (see FolderFormRequest)
        public string? SubFolders { get; set; }
        public IFormFile? File { get; set; }
        public bool? RemoveImage { get; set; }

        public SubFolderRequest ToRequest() => new()
        {
            FolderId = FolderId,
            Name = Name,
            SubFolders = FolderFormParser.ParseSubFolders(SubFolders),
            File = File,
            RemoveImage = RemoveImage
        };
    }

    internal static class FolderFormParser
    {
        private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

        public static List<SubFolderNodeRequest>? ParseSubFolders(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<SubFolderNodeRequest>>(json, JsonOptions);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new InvalidOperationException("subFolders must be a JSON array, e.g. [{\"name\":\"2023\",\"subFolders\":[\"Paper 1\"]}]");
            }
        }
    }

    public class SubFolderResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        // The parent: a folder, or another sub folder
        public string FolderId { get; set; } = string.Empty;
        public List<SubFolderResponse> SubFolders { get; set; } = new();
    }

    public class FolderResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public List<SubFolderResponse> SubFolders { get; set; } = new();
    }

    // One level of a quiz's sub folder path, with the picture for that level
    public class FolderNodeInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
    }

    // Implemented by the quiz and the student quiz response so both get the folder pictures the same way
    public interface IFolderTrail
    {
        string? FolderId { get; }
        string? SubFolderId { get; }
        string? FolderImageUrl { get; set; }
        string? SubFolderImageUrl { get; set; }
        List<FolderNodeInfo>? SubFolderPathNodes { get; set; }
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
