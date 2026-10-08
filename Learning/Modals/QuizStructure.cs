using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    public abstract class QuizStructureItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    // One standard offered by a subject (or category), and the parts it is split into FOR THAT subject.
    // Parts live here, not on the standard, so Tamil can have Part-1..3 in standard 6 while Maths has none.
    [BsonIgnoreExtraElements]
    public class QuizStandardLink
    {
        [BsonElement("standardId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string StandardId { get; set; } = string.Empty;

        [BsonElement("partIds")]
        public List<string> PartIds { get; set; } = new();
    }

    // A quiz subject (Tamil, GK ...). A subject with categories gets its standards through them;
    // a subject without categories lists its standards directly.
    [BsonIgnoreExtraElements]
    public class QuizSubject : QuizStructureItem
    {
        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("standards")]
        public List<QuizStandardLink> Standards { get; set; } = new();

        // Old shape (standards shared the same parts everywhere) - only read by the one-time migration
        [BsonElement("standardIds")]
        public List<string>? LegacyStandardIds { get; set; }
    }

    // A category under one subject (GK -> Social / Science)
    [BsonIgnoreExtraElements]
    public class QuizCategory : QuizStructureItem
    {
        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("subjectId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string SubjectId { get; set; } = string.Empty;

        [BsonElement("standards")]
        public List<QuizStandardLink> Standards { get; set; } = new();

        [BsonElement("standardIds")]
        public List<string>? LegacyStandardIds { get; set; }
    }

    // A school standard (6, 7 ... 12)
    [BsonIgnoreExtraElements]
    public class QuizStandard : QuizStructureItem
    {
        [BsonElement("number")]
        public int Number { get; set; }

        [BsonElement("partIds")]
        public List<string>? LegacyPartIds { get; set; }
    }

    // A part name (Part-1, Part-2 ...)
    [BsonIgnoreExtraElements]
    public class QuizPart : QuizStructureItem
    {
        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;
    }
}
