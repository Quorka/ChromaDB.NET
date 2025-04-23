using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromaDB.NET
{
    /// <summary>
    /// Custom JSON converter specifically for ChromaDB 'whereFilter' dictionaries.
    /// Handles nested operators ($eq, $gt, $in, $and, $or, etc.) correctly,
    /// ensuring the output JSON matches the structure expected by the Rust backend's
    /// metadata filtering logic (related to MetadataValue, MetadataComparison, Where enums).
    /// </summary>
    public class WhereFilterConverter : JsonConverter<Dictionary<string, object>>
    {
        // Define known ChromaDB filter operators
        private static readonly HashSet<string> LogicalOperators = new HashSet<string> { "$and", "$or" };
        private static readonly HashSet<string> ComparisonOperators = new HashSet<string> { "$eq", "$ne", "$gt", "$gte", "$lt", "$lte" };
        private static readonly HashSet<string> SetOperators = new HashSet<string> { "$in", "$nin" };

        public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Reading is less critical for serialization, but implement basic structure
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token for whereFilter");
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
                reader.Read(); // Move to the value
                dictionary.Add(propertyName, ReadValue(ref reader, options));
            }

            throw new JsonException("Expected EndObject token");
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var kvp in value)
            {
                string key = kvp.Key;
                object val = kvp.Value;

                writer.WritePropertyName(key);

                // Handle top-level logical operators ($and, $or)
                if (LogicalOperators.Contains(key))
                {
                    if (val is IEnumerable<Dictionary<string, object>> logicalList)
                    {
                        writer.WriteStartArray();
                        foreach (var innerFilter in logicalList)
                        {
                            // Recursively serialize each filter clause in the list
                            JsonSerializer.Serialize(writer, innerFilter, options);
                        }
                        writer.WriteEndArray();
                    }
                    else
                    {
                        throw new JsonException($"Value for logical operator '{key}' must be an IEnumerable<Dictionary<string, object>>.");
                    }
                }
                // Handle field filters (implicit equality or explicit operators)
                else
                {
                    WriteFieldValue(writer, val, options);
                }
            }

            writer.WriteEndObject();
        }

        private object ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            // Basic recursive read logic
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
                case JsonTokenType.StartArray:
                    var list = new List<object>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        list.Add(ReadValue(ref reader, options));
                    }
                    return list;
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out long l)) return l;
                    return reader.GetDouble();
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Null:
                    return null; // Note: Check if null is valid in ChromaDB filters
                default:
                    throw new JsonException($"Unsupported token type: {reader.TokenType}");
            }
        }

        private void WriteFieldValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            // Check if the value represents an explicit operator clause (e.g., {"$gt": 10})
            if (value is IDictionary<string, object> operatorDict && operatorDict.Count == 1)
            {
                var op = operatorDict.Keys.First();
                var opValue = operatorDict.Values.First();

                // Handle comparison operators ($eq, $ne, $gt, etc.)
                if (ComparisonOperators.Contains(op))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(op);
                    WritePrimitiveValue(writer, opValue, $"operator '{op}'");
                    writer.WriteEndObject();
                    return; // Handled
                }
                // Handle set operators ($in, $nin)
                else if (SetOperators.Contains(op))
                {
                    if (opValue is IEnumerable setValues && !(opValue is string)) // Ensure it's not a string
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName(op);
                        writer.WriteStartArray();
                        foreach (var item in setValues)
                        {
                            WritePrimitiveValue(writer, item, $"array element for operator '{op}'");
                        }
                        writer.WriteEndArray();
                        writer.WriteEndObject();
                        return; // Handled
                    }
                    else
                    {
                        throw new JsonException($"Value for set operator '{op}' must be an IEnumerable (array/list) of primitive types.");
                    }
                }
                // If it's a dictionary but not a recognized operator, fall through to primitive serialization
                // (This might occur if a field's value is genuinely a complex object, though unlikely for filters)
            }

            // Default: Assume implicit equality, serialize as primitive MetadataValue
            WritePrimitiveValue(writer, value, "filter value (implicit equality)");
        }

        private void WritePrimitiveValue(Utf8JsonWriter writer, object value, string context)
        {
            // Ensure the value is one of the types compatible with Rust's MetadataValue
            if (value == null) { writer.WriteNullValue(); } // Check if null is actually allowed by Rust
            else if (value is string s) { writer.WriteStringValue(s); }
            else if (value is bool b) { writer.WriteBooleanValue(b); }
            else if (value is int i) { writer.WriteNumberValue(i); }
            else if (value is long l) { writer.WriteNumberValue(l); }
            else if (value is float f) { writer.WriteNumberValue(f); }
            else if (value is double d) { writer.WriteNumberValue(d); }
            else if (value is decimal dec) { writer.WriteNumberValue(dec); } // Check if decimal precision matches Rust f64
            else if (value is DateTime dt) { writer.WriteStringValue(dt.ToString("o")); } // Serialize dates as ISO strings
            else if (value is DateTimeOffset dto) { writer.WriteStringValue(dto.ToString("o")); }
            else if (value is Guid g) { writer.WriteStringValue(g.ToString()); }
            else
            {
                // Throw error for unsupported types in filters
                throw new JsonException($"Unsupported type '{value.GetType().Name}' found for {context}. Only bool, long, double, string, or arrays for $in/$nin are supported in where filters.");
            }
        }
    }
}