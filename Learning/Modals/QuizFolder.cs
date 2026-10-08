using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    // A folder that groups "previousYear" quizzes (e.g. an exam), optionally split into sub folders (e.g. a year)
    [BsonIgnoreExtraElements]
    public class QuizFolder : QuizStructureItem
    {
        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;
    }

    [BsonIgnoreExtraElements]
    public class QuizSubFolder : QuizStructureItem
    {
        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("folderId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string FolderId { get; set; } = string.Empty;
    }
}
