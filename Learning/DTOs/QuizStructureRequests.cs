using System.Text.Json;
using System.Text.Json.Serialization;

namespace LearningBackendAPI.DTOs
{
    // Links are written by value, not by id: standards as numbers (6, 7 ...), parts by name ("Part-1"),
    // a category's subject by name ("GK"). Parts are chosen per subject and standard, e.g.
    //   "standards": [ { "standard": 6, "parts": ["Part-1", "Part-2"] }, { "standard": 12 } ]
    // On update, a null "standards" leaves the existing links unchanged; a list replaces them all
    // (a standard sent without parts has no parts for this subject).
    // Each entry may be a bare number (6 - the standard, no parts) or an object ({ "standard": 6, "parts": ["Part-1"] })
    [JsonConverter(typeof(QuizStandardLinkRequestConverter))]
    public class QuizStandardLinkRequest
    {
        public int? Standard { get; set; }
        public List<string>? Parts { get; set; }
    }

    public class QuizStandardLinkRequestConverter : JsonConverter<QuizStandardLinkRequest>
    {
        public override QuizStandardLinkRequest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;

                case JsonTokenType.Number:
                    return new QuizStandardLinkRequest { Standard = reader.GetInt32() };

                case JsonTokenType.String when int.TryParse(reader.GetString(), out var number):
                    return new QuizStandardLinkRequest { Standard = number };

                case JsonTokenType.StartObject:
                    var link = new QuizStandardLinkRequest();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        var property = reader.GetString();
                        reader.Read();
                        if (string.Equals(property, "standard", StringComparison.OrdinalIgnoreCase))
                        {
                            link.Standard = reader.TokenType == JsonTokenType.Null ? null
                                : reader.TokenType == JsonTokenType.String && int.TryParse(reader.GetString(), out var n) ? n
                                : reader.GetInt32();
                        }
                        else if (string.Equals(property, "parts", StringComparison.OrdinalIgnoreCase))
                        {
                            link.Parts = JsonSerializer.Deserialize<List<string>>(ref reader, options);
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                    return link;

                default:
                    throw new JsonException("Each standard must be a number (6) or an object like { \"standard\": 6, \"parts\": [\"Part-1\"] }");
            }
        }

        public override void Write(Utf8JsonWriter writer, QuizStandardLinkRequest value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value.Standard != null)
            {
                writer.WriteNumber("standard", value.Standard.Value);
            }
            writer.WritePropertyName("parts");
            JsonSerializer.Serialize(writer, value.Parts, options);
            writer.WriteEndObject();
        }
    }

    public class QuizSubjectRequest
    {
        public string? Name { get; set; }
        public List<QuizStandardLinkRequest>? Standards { get; set; }
    }

    public class QuizCategoryRequest
    {
        public string? Name { get; set; }
        // Required on create; the subject of an existing category can't be changed
        public string? Subject { get; set; }
        public List<QuizStandardLinkRequest>? Standards { get; set; }
    }

    public class QuizStandardRequest
    {
        public int? Standard { get; set; }
    }

    public class QuizPartRequest
    {
        public string? Name { get; set; }
    }

    public class QuizStandardLinkResponse
    {
        public int Standard { get; set; }
        public List<string> Parts { get; set; } = new();
    }

    public class QuizSubjectResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<QuizStandardLinkResponse> Standards { get; set; } = new();
    }

    public class QuizCategoryResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public List<QuizStandardLinkResponse> Standards { get; set; } = new();
    }

    public class QuizStandardResponse
    {
        public string Id { get; set; } = string.Empty;
        public int Standard { get; set; }
    }

    public class QuizPartResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
