using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChromaDB.NET.Tests
{
    [TestClass]
    public class MetadataConverterTests
    {
        [TestMethod]
        public void SerializeMetadata_NullMetadata_ReturnsNull()
        {
            // Act
            string result = MetadataConverter.SerializeMetadata(null);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void SerializeMetadata_EmptyDictionary_ReturnsEmptyObject()
        {
            // Arrange
            var metadata = new Dictionary<string, object>();

            // Act
            string result = MetadataConverter.SerializeMetadata(metadata);

            // Assert
            Assert.AreEqual("{}", result);
        }

        [TestMethod]
        public void SerializeMetadata_PrimitiveValues_ReturnsCorrectJson()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                ["string"] = "test",
                ["int"] = 42,
                ["bool"] = true,
                ["null"] = null,
                ["float"] = 3.14f,
                ["double"] = 2.718281828459,
                ["long"] = 9223372036854775807L,
                ["dateTime"] = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                ["guid"] = Guid.Parse("550e8400-e29b-41d4-a716-446655440000")
            };

            // Act
            string result = MetadataConverter.SerializeMetadata(metadata);
            var resultDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(result);

            // Assert
            Assert.IsNotNull(resultDict);
            Assert.AreEqual("test", resultDict["string"].GetString());
            Assert.AreEqual(42, resultDict["int"].GetInt32());
            Assert.IsTrue(resultDict["bool"].GetBoolean());
            Assert.AreEqual(JsonValueKind.Null, resultDict["null"].ValueKind);
            Assert.AreEqual(3.14f, resultDict["float"].GetSingle());
            Assert.AreEqual(2.718281828459, resultDict["double"].GetDouble(), 0.0000000001);
            Assert.AreEqual(9223372036854775807L, resultDict["long"].GetInt64());
            Assert.AreEqual("2023-01-01T12:00:00.0000000Z", resultDict["dateTime"].GetString());
            Assert.AreEqual("550e8400-e29b-41d4-a716-446655440000", resultDict["guid"].GetString());
        }

        [TestMethod]
        public void DeserializeMetadata_NullOrEmptyJson_ReturnsEmptyDictionary()
        {
            // Act
            var result1 = MetadataConverter.DeserializeMetadata(null);
            var result2 = MetadataConverter.DeserializeMetadata(string.Empty);
            var result3 = MetadataConverter.DeserializeMetadata(" ");

            // Assert
            Assert.IsNotNull(result1);
            Assert.AreEqual(0, result1.Count);
            
            Assert.IsNotNull(result2);
            Assert.AreEqual(0, result2.Count);
            
            Assert.IsNotNull(result3);
            Assert.AreEqual(0, result3.Count);
        }

        [TestMethod]
        public void DeserializeMetadata_ValidJson_ReturnsCorrectDictionary()
        {
            // Arrange
            string json = @"{""string"":""test"",""int"":42,""bool"":true,""null"":null}";

            // Act
            var result = MetadataConverter.DeserializeMetadata(json);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.Count);
            
            // Test string value - Note that System.Text.Json deserializes this as JsonElement
            Assert.IsInstanceOfType(result["string"], typeof(JsonElement));
            Assert.AreEqual("test", ((JsonElement)result["string"]).GetString());
            
            // Test numeric value (System.Text.Json deserializes as JsonElement which we verify)
            Assert.IsInstanceOfType(result["int"], typeof(JsonElement));
            Assert.AreEqual(42, ((JsonElement)result["int"]).GetInt32());
            
            // Test boolean value
            Assert.IsInstanceOfType(result["bool"], typeof(JsonElement));
            Assert.AreEqual(true, ((JsonElement)result["bool"]).GetBoolean());
            
            // For null values, simply check that it's null
            Assert.IsNull(result["null"]);
        }

        [TestMethod]
        public void DeserializeMetadata_InvalidJson_ReturnsEmptyDictionary()
        {
            // Arrange
            string invalidJson = "{ this is not valid JSON }";

            // Act
            var result = MetadataConverter.DeserializeMetadata(invalidJson);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void MetadataDictionaryConverter_WriteValue_HandlesAllSupportedTypes()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                ["string"] = "test",
                ["int"] = 42,
                ["bool"] = true,
                ["null"] = null,
                ["float"] = 3.14f,
                ["double"] = 2.718281828459,
                ["decimal"] = 12345.6789m,
                ["dateTime"] = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                ["dateTimeOffset"] = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero),
                ["guid"] = Guid.Parse("550e8400-e29b-41d4-a716-446655440000")
            };

            // Act
            string result = JsonSerializer.Serialize(metadata, MetadataConverter.SerializerOptions);
            var deserializedResult = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(result);

            // Assert
            Assert.IsNotNull(deserializedResult);
            Assert.AreEqual(10, deserializedResult.Count);
            
            Assert.AreEqual("test", deserializedResult["string"].GetString());
            Assert.AreEqual(42, deserializedResult["int"].GetInt32());
            Assert.IsTrue(deserializedResult["bool"].GetBoolean());
            Assert.AreEqual(JsonValueKind.Null, deserializedResult["null"].ValueKind);
            Assert.AreEqual(3.14f, deserializedResult["float"].GetSingle());
            Assert.AreEqual(2.718281828459, deserializedResult["double"].GetDouble(), 0.0000000001);
            Assert.AreEqual(12345.6789m, deserializedResult["decimal"].GetDecimal());
            Assert.AreEqual("2023-01-01T12:00:00.0000000Z", deserializedResult["dateTime"].GetString());
            Assert.AreEqual("2023-01-01T12:00:00.0000000+00:00", deserializedResult["dateTimeOffset"].GetString());
            Assert.AreEqual("550e8400-e29b-41d4-a716-446655440000", deserializedResult["guid"].GetString());
        }

        [TestMethod]
        public void MetadataDictionaryConverter_ReadValue_ParsesAllSupportedTypes()
        {
            // Arrange
            string json = @"{
                ""string"": ""test"",
                ""int"": 42,
                ""bool"": true,
                ""null"": null,
                ""double"": 3.14159
            }";

            // Act
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, MetadataConverter.SerializerOptions);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(5, result.Count);
            
            Assert.AreEqual("test", result["string"]);
            Assert.AreEqual(42L, result["int"]); // Numbers are deserialized as long by default
            Assert.AreEqual(true, result["bool"]);
            Assert.IsNull(result["null"]);
            Assert.IsTrue(Math.Abs(3.14159 - (double)result["double"]) < 0.00001);
        }

        [TestMethod]
        [ExpectedException(typeof(JsonException))]
        public void MetadataDictionaryConverter_Read_ThrowsOnInvalidStartToken()
        {
            // Arrange
            string json = @"[""not an object""]"; // Array instead of object

            // Act - should throw
            JsonSerializer.Deserialize<Dictionary<string, object>>(json, MetadataConverter.SerializerOptions);
        }
    }
}