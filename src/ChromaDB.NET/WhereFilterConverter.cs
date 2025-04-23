using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public class WhereFilterConverter : JsonConverter<WhereFilter>
    {
        // Define known ChromaDB filter operators
        private static readonly HashSet<string> LogicalOperators = new HashSet<string> { "$and", "$or" };
        private static readonly HashSet<string> ComparisonOperators = new HashSet<string> { "$eq", "$ne", "$gt", "$gte", "$lt", "$lte" };
        private static readonly HashSet<string> SetOperators = new HashSet<string> { "$in", "$nin" };

        public override WhereFilter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Deserialization is not implemented as filters are typically constructed in code
            throw new NotImplementedException("Deserialization of WhereFilter is not implemented.");
        }

        public override void Write(Utf8JsonWriter writer, WhereFilter value, JsonSerializerOptions options)
        {
            var filterDict = value.FilterDictionary;
            var combineWithOr = value.CombineWithOr;

            if (filterDict.Count == 0)
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
                return;
            }

            // Handle explicitly provided $and or $or keys first
            if (filterDict.ContainsKey("$and") || filterDict.ContainsKey("$or"))
            {
                if (filterDict.Count > 1)
                {
                    // If $and/$or is explicitly set with other top-level filters,
                    // serialize just the explicit logical operator for simplicity
                    var logicalKey = filterDict.ContainsKey("$and") ? "$and" : "$or";
                    writer.WriteStartObject();
                    writer.WritePropertyName(logicalKey);
                    JsonSerializer.Serialize(writer, filterDict[logicalKey], options);
                    writer.WriteEndObject();
                    return;
                }
                // If only $and or $or exists, fall through to standard dictionary serialization
            }
            else if (filterDict.Count > 1)
            {
                // Multiple filter conditions need to be wrapped with $and or $or
                string logicalOperator = combineWithOr ? "$or" : "$and";

                writer.WriteStartObject();
                writer.WritePropertyName(logicalOperator);
                writer.WriteStartArray();

                foreach (var kvp in filterDict)
                {
                    // Each condition becomes its own object within the array
                    writer.WriteStartObject();
                    writer.WritePropertyName(kvp.Key);
                    WriteValue(writer, kvp.Value, options);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                return;
            }

            // Handle single filter condition or explicitly provided $and/$or with no other filters
            writer.WriteStartObject();
            foreach (var kvp in filterDict)
            {
                writer.WritePropertyName(kvp.Key);
                if (LogicalOperators.Contains(kvp.Key))
                {
                    // Explicitly handle $and/$or arrays
                    JsonSerializer.Serialize(writer, kvp.Value, options);
                }
                else
                {
                    // Write field value (implicit equality or explicit operator)
                    WriteValue(writer, kvp.Value, options);
                }
            }
            writer.WriteEndObject();
        }

        private void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            // Check if the value represents an explicit operator clause (e.g., {"$gt": 10})
            if (value is IDictionary<string, object> operatorDict)
            {
                writer.WriteStartObject();
                foreach (var kvp in operatorDict)
                {
                    writer.WritePropertyName(kvp.Key);
                    if (SetOperators.Contains(kvp.Key) && kvp.Value is IEnumerable setValues && !(kvp.Value is string))
                    {
                        // Handle array values for $in/$nin operators
                        writer.WriteStartArray();
                        foreach (var item in setValues)
                        {
                            WritePrimitiveValue(writer, item);
                        }
                        writer.WriteEndArray();
                    }
                    else
                    {
                        // Handle primitive values for comparison operators
                        WritePrimitiveValue(writer, kvp.Value);
                    }
                }
                writer.WriteEndObject();
            }
            else
            {
                // Write primitive value directly (for implicit equality)
                WritePrimitiveValue(writer, value);
            }
        }

        private void WritePrimitiveValue(Utf8JsonWriter writer, object value)
        {
            // Ensure the value is one of the types compatible with Rust's MetadataValue
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else if (value is string s)
            {
                writer.WriteStringValue(s);
            }
            else if (value is bool b)
            {
                writer.WriteBooleanValue(b);
            }
            else if (value is int i)
            {
                writer.WriteNumberValue(i);
            }
            else if (value is long l)
            {
                writer.WriteNumberValue(l);
            }
            else if (value is float f)
            {
                writer.WriteNumberValue(f);
            }
            else if (value is double d)
            {
                writer.WriteNumberValue(d);
            }
            else if (value is decimal dec)
            {
                writer.WriteNumberValue(dec);
            }
            else if (value is DateTime dt)
            {
                writer.WriteStringValue(dt.ToString("o"));
            }
            else if (value is DateTimeOffset dto)
            {
                writer.WriteStringValue(dto.ToString("o"));
            }
            else if (value is Guid g)
            {
                writer.WriteStringValue(g.ToString());
            }
            else
            {
                throw new JsonException($"Unsupported type '{value.GetType().Name}' found in where filter. Only bool, numeric types, string, or arrays for $in/$nin are supported.");
            }
        }
    }
}