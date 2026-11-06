using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    // A PDF filed under a folder (and sub folder) from GET /api/Folder. A student sees it once a verified
    // syllabus wise payment covers its folder.
    [BsonIgnoreExtraElements]
    public class SyllabusMaterial
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("description")]
        public string? Description { get; set; }

        [BsonElement("folderId")]
        public string FolderId { get; set; } = string.Empty;

        [BsonElement("subFolderId")]
        public string? SubFolderId { get; set; }

        [BsonElement("pdfUrl")]
        public string PdfUrl { get; set; } = string.Empty;

        [BsonElement("pdfKey")]
        public string PdfKey { get; set; } = string.Empty;

        // The uploaded file's own name, e.g. "TNPSC-History.pdf"
        [BsonElement("pdfFileName")]
        public string PdfFileName { get; set; } = string.Empty;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        // Looked up on read from the folder tree, so a renamed folder shows its new name
        [BsonIgnore]
        public string? FolderName { get; set; }

        [BsonIgnore]
        public string? SubFolderName { get; set; }

        // Every sub folder from the folder's first level down to SubFolderName, joined by "/"
        [BsonIgnore]
        public string? SubFolderPath { get; set; }
    }
}
