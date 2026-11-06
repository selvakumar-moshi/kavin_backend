using System.Text.Json;
using System.Text.Json.Serialization;

namespace LearningBackendAPI.DTOs
{
    // Lets a leaf be sent as a plain string ("lesson-1") as well as { "name": "lesson-1", "subFolders": [...] }
    public class SubFolderNodeRequestConverter : JsonConverter<SubFolderNodeRequest>
    {
        public override SubFolderNodeRequest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.String:
                    return new SubFolderNodeRequest { Name = reader.GetString() };
                case JsonTokenType.StartObject:
                    var node = new SubFolderNodeRequest();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        var property = reader.GetString();
                        reader.Read();
                        if (string.Equals(property, "name", StringComparison.OrdinalIgnoreCase))
                        {
                            node.Name = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                        }
                        else if (string.Equals(property, "subFolders", StringComparison.OrdinalIgnoreCase))
                        {
                            node.SubFolders = JsonSerializer.Deserialize<List<SubFolderNodeRequest>>(ref reader, options);
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                    return node;
                default:
                    throw new JsonException("A sub folder must be a name or an object with a name");
            }
        }

        public override void Write(Utf8JsonWriter writer, SubFolderNodeRequest value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("name", value.Name);
            writer.WritePropertyName("subFolders");
            JsonSerializer.Serialize(writer, value.SubFolders, options);
            writer.WriteEndObject();
        }
    }
}
