using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace ChromaDB.NET
{
    /// <summary>
    /// Converter for ChromaDB metadata to ensure compatibility with Rust backend's expected types
    /// </summary>
    internal static class MetadataConverter
    {
        /// <summary>
        /// System.Text.Json options for serializing metadata
        /// </summary>
        public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            Converters = { new MetadataDictionaryConverter() }
        };

        /// <summary>
        /// Serialize metadata dictionary to JSON string that's compatible with ChromaDB's Rust backend
        /// </summary>
        public static string SerializeMetadata(Dictionary<string, object> metadata)
        {
            if (metadata == null)
                return null;

            return JsonSerializer.Serialize(metadata, SerializerOptions);
        }

        /// <summary>
        /// Deserialize JSON string to metadata dictionary
        /// </summary>
        public static Dictionary<string, object> DeserializeMetadata(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, object>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            }
            catch
            {
                // Fall back to empty dictionary on parse error
                return new Dictionary<string, object>();
            }
        }
    }

    /// <summary>
    /// Custom JSON converter for metadata dictionary to ensure compatibility with Rust's MetadataValue enum
    /// </summary>
    public class MetadataDictionaryConverter : JsonConverter<Dictionary<string, object>>
    {
        public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token");
            }

            var dictionary = new Dictionary<string, object>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return dictionary;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected PropertyName token");
                }

                string propertyName = reader.GetString();

                reader.Read();
                dictionary.Add(propertyName, ExtractValue(ref reader));
            }

            throw new JsonException("Expected EndObject token");
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var item in value)
            {
                writer.WritePropertyName(item.Key);
                WriteValue(writer, item.Value);
            }

            writer.WriteEndObject();
        }

        private static object ExtractValue(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out long longValue))
                    {
                        return longValue;
                    }
                    return reader.GetDouble();
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new JsonException($"Unsupported token type: {reader.TokenType}");
            }
        }

        private static void WriteValue(Utf8JsonWriter writer, object value)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case bool boolValue:
                    writer.WriteBooleanValue(boolValue);
                    break;
                case string stringValue:
                    writer.WriteStringValue(stringValue);
                    break;
                case int intValue:
                    writer.WriteNumberValue(intValue);
                    break;
                case long longValue:
                    writer.WriteNumberValue(longValue);
                    break;
                case double doubleValue:
                    writer.WriteNumberValue(doubleValue);
                    break;
                case float floatValue:
                    writer.WriteNumberValue(floatValue);
                    break;
                case decimal decimalValue:
                    writer.WriteNumberValue(decimalValue);
                    break;
                case DateTime dateTimeValue:
                    writer.WriteStringValue(dateTimeValue.ToString("o")); // ISO 8601 format
                    break;
                case DateTimeOffset dateTimeOffsetValue:
                    writer.WriteStringValue(dateTimeOffsetValue.ToString("o")); // ISO 8601 format
                    break;
                case Guid guidValue:
                    writer.WriteStringValue(guidValue.ToString());
                    break;
                default:
                    // For any other types, convert to string via JSON serialization
                    writer.WriteStringValue(JsonSerializer.Serialize(value));
                    break;
            }
        }

    }
}