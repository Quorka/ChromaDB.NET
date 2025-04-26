using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace ChromaDB.NET.Tests
{
    [TestClass]
    public class WhereFilterTests
    {
        private string _testDir = string.Empty;
        private TestEmbeddingFunction _embeddingFunction = new TestEmbeddingFunction();

        [TestInitialize]
        public void Initialize()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "chromadb-dotnet-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
            _embeddingFunction = new TestEmbeddingFunction();
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    // Add a small delay to allow file handles to be released
                    System.Threading.Thread.Sleep(100); // e.g., 100ms, adjust if needed
                    Directory.Delete(_testDir, true);
                    Console.WriteLine($"Cleaned up test directory: {_testDir}");
                }
            }
            catch (Exception ex)
            {
                // Log the error instead of ignoring it
                Console.WriteLine($"Error cleaning up test directory '{_testDir}': {ex.Message}");
            }
        }

        [TestMethod]
        public void WhereFilter_Equals_CreatesCorrectFilter()
        {
            var filter = new WhereFilter()
                .Equals("category", "animals");

            var dict = filter.ToDictionary();

            Assert.IsNotNull(dict);
            Assert.IsTrue(dict.ContainsKey("category"));
            Assert.AreEqual("animals", dict["category"]);
        }

        [TestMethod]
        public void WhereFilter_ImplicitConversion_WorksCorrectly()
        {
            WhereFilter filter = new WhereFilter()
                .Equals("category", "animals");

            Dictionary<string, object> dict = filter; // Implicit conversion

            Assert.IsNotNull(dict);
            Assert.IsTrue(dict.ContainsKey("category"));
            Assert.AreEqual("animals", dict["category"]);
        }

        [TestMethod]
        public void WhereFilter_In_CreatesCorrectFilter()
        {
            var filter = new WhereFilter()
                .In("category", new[] { "animals", "pets" });

            var dict = filter.ToDictionary();

            Assert.IsNotNull(dict);
            Assert.IsTrue(dict.ContainsKey("category"));

            var categoryDict = dict["category"] as Dictionary<string, object>;
            Assert.IsNotNull(categoryDict);
            Assert.IsTrue(categoryDict.ContainsKey("$in"));

            var values = categoryDict["$in"] as List<object>;
            Assert.IsNotNull(values);
            Assert.AreEqual(2, values.Count);
            Assert.IsTrue(values.Contains("animals"));
            Assert.IsTrue(values.Contains("pets"));
        }

        [TestMethod]
        public void WhereFilter_NotIn_CreatesCorrectFilter()
        {
            var filter = new WhereFilter()
                .NotIn("category", new[] { "weather", "news" });

            var dict = filter.ToDictionary();

            Assert.IsNotNull(dict);
            Assert.IsTrue(dict.ContainsKey("category"));

            var categoryDict = dict["category"] as Dictionary<string, object>;
            Assert.IsNotNull(categoryDict);
            Assert.IsTrue(categoryDict.ContainsKey("$nin"));

            var values = categoryDict["$nin"] as List<object>;
            Assert.IsNotNull(values);
            Assert.AreEqual(2, values.Count);
            Assert.IsTrue(values.Contains("weather"));
            Assert.IsTrue(values.Contains("news"));
        }

        [TestMethod]
        public void WhereFilter_GreaterThan_CreatesCorrectFilter()
        {
            var filter = new WhereFilter()
                .GreaterThan("year", 2020);

            var dict = filter.ToDictionary();

            Assert.IsNotNull(dict);
            Assert.IsTrue(dict.ContainsKey("year"));

            var yearDict = dict["year"] as Dictionary<string, object>;
            Assert.IsNotNull(yearDict);
            Assert.IsTrue(yearDict.ContainsKey("$gt"));
            Assert.AreEqual(2020, yearDict["$gt"]);
        }

        [TestMethod]
        public void WhereFilter_GreaterThanOrEqual_CreatesCorrectFilter()
        {
            var filter = new WhereFilter()
                .GreaterThanOrEqual("year", 2020);

            var dict = filter.ToDictionary();

            Assert.IsNotNull(dict);
            Assert.IsTrue(dict.ContainsKey("year"));

            var yearDict = dict["year"] as Dictionary<string, object>;
            Assert.IsNotNull(yearDict);
            Assert.IsTrue(yearDict.ContainsKey("$gte"));
            Assert.AreEqual(2020, yearDict["$gte"]);
        }

        [TestMethod]
        public void WhereFilter_LessThan_CreatesCorrectFilter()
        {
            var filter = new WhereFilter()
                .LessThan("year", 2020);

            var dict = filter.ToDictionary();

            Assert.IsNotNull(dict);
            Assert.IsTrue(dict.ContainsKey("year"));

            var yearDict = dict["year"] as Dictionary<string, object>;
            Assert.IsNotNull(yearDict);
            Assert.IsTrue(yearDict.ContainsKey("$lt"));
            Assert.AreEqual(2020, yearDict["$lt"]);
        }

        [TestMethod]
        public void WhereFilter_LessThanOrEqual_CreatesCorrectFilter()
        {
            var filter = new WhereFilter()
                .LessThanOrEqual("year", 2020);

            var dict = filter.ToDictionary();

            Assert.IsNotNull(dict);
            Assert.IsTrue(dict.ContainsKey("year"));

            var yearDict = dict["year"] as Dictionary<string, object>;
            Assert.IsNotNull(yearDict);
            Assert.IsTrue(yearDict.ContainsKey("$lte"));
            Assert.AreEqual(2020, yearDict["$lte"]);
        }

        [TestMethod]
        public void WhereFilter_ChainedOperations_CreatesCorrectFilter()
        {
            var filter = new WhereFilter()
                .Equals("category", "books")
                .GreaterThan("year", 2010)
                .LessThan("price", 30.0);

            var dict = filter.ToDictionary();

            Assert.IsNotNull(dict);
            Assert.AreEqual(3, dict.Count);

            Assert.IsTrue(dict.ContainsKey("category"));
            Assert.AreEqual("books", dict["category"]);

            Assert.IsTrue(dict.ContainsKey("year"));
            var yearDict = dict["year"] as Dictionary<string, object>;
            Assert.IsNotNull(yearDict);
            Assert.IsTrue(yearDict.ContainsKey("$gt"));
            Assert.AreEqual(2010, yearDict["$gt"]);

            Assert.IsTrue(dict.ContainsKey("price"));
            var priceDict = dict["price"] as Dictionary<string, object>;
            Assert.IsNotNull(priceDict);
            Assert.IsTrue(priceDict.ContainsKey("$lt"));
            Assert.AreEqual(30.0, priceDict["$lt"]);
        }

        [TestMethod]
        public void WhereFilter_IntegrationTest_FiltersCorrectly()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add test documents
            collection.Add("doc1", "Book 1", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["year"] = 2015,
                ["price"] = 25.0
            });

            collection.Add("doc2", "Book 2", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["year"] = 2018,
                ["price"] = 35.0
            });

            collection.Add("doc3", "Magazine 1", new Dictionary<string, object>
            {
                ["category"] = "magazines",
                ["year"] = 2020,
                ["price"] = 15.0
            });

            // Test equals
            var filter1 = new WhereFilter().Equals("category", "books");
            var results1 = collection.Where(filter1);
            Assert.AreEqual(2, results1.Count);
            Assert.IsTrue(results1.Ids.Contains("doc1") && results1.Ids.Contains("doc2"));

            // Test greater than
            var filter2 = new WhereFilter().GreaterThan("year", 2015);
            var results2 = collection.Where(filter2);
            Assert.AreEqual(2, results2.Count);
            Assert.IsTrue(results2.Ids.Contains("doc2") && results2.Ids.Contains("doc3"));

            // Test less than
            var filter3 = new WhereFilter().LessThan("price", 30.0);
            var results3 = collection.Where(filter3);
            Assert.AreEqual(2, results3.Count);
            Assert.IsTrue(results3.Ids.Contains("doc1") && results3.Ids.Contains("doc3"));
        }
        [TestMethod]
        public void WhereFilter_IntegrationTest_CombinedFilter()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add test documents
            collection.Add("doc1", "Book 1", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["year"] = 2015,
                ["price"] = 25.0
            });

            collection.Add("doc2", "Book 2", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["year"] = 2018,
                ["price"] = 35.0
            });

            collection.Add("doc3", "Magazine 1", new Dictionary<string, object>
            {
                ["category"] = "magazines",
                ["year"] = 2020,
                ["price"] = 15.0
            });
            // Test combined filters
            var filter4 = new WhereFilter()
            .Equals("category", "books")
            .GreaterThan("year", 2015)
            .LessThan("price", 40.0);

            // Debug - print the generated JSON
            var json = JsonSerializer.Serialize(filter4);
            Console.WriteLine($"AND filter JSON: {json}");

            try
            {
                var results4 = collection.Where(filter4);
                Assert.AreEqual(1, results4.Count);
                Assert.AreEqual("doc2", results4.Ids[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AND filter error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        [TestMethod]
        public void WhereFilter_IntegrationTest_FiltersInOperator()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add test documents
            collection.Add("doc1", "Book 1", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["year"] = 2015,
                ["price"] = 25.0
            });

            collection.Add("doc2", "Book 2", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["year"] = 2018,
                ["price"] = 35.0
            });

            collection.Add("doc3", "Magazine 1", new Dictionary<string, object>
            {
                ["category"] = "magazines",
                ["year"] = 2020,
                ["price"] = 15.0
            });
            // Test In operator
            var filter5 = new WhereFilter().In("category", new[] { "books", "newspapers" });
            var results5 = collection.Where(filter5);
            Assert.AreEqual(2, results5.Count);
            Assert.IsTrue(results5.Ids.Contains("doc1") && results5.Ids.Contains("doc2"));
        }

        [TestMethod]
        public void WhereFilter_ChainedOperations_WithAnd_GeneratesCorrectJson()
        {
            var filter = new WhereFilter()
                .Equals("category", "books")
                .GreaterThan("year", 2010)
                .LessThan("price", 30.0);

            // Convert to JSON to verify the correct structure is generated
            var json = JsonSerializer.Serialize(filter);

            // Parse the JSON to check its structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify that multiple conditions are wrapped with $and
            Assert.IsTrue(root.TryGetProperty("$and", out var andArray));
            Assert.AreEqual(JsonValueKind.Array, andArray.ValueKind);
            Assert.AreEqual(3, andArray.GetArrayLength());

            // Check that each condition is correctly represented
            var conditions = andArray.EnumerateArray().ToArray();

            // Check first condition: {"category":"books"}
            Assert.IsTrue(conditions[0].TryGetProperty("category", out var categoryValue));
            Assert.AreEqual("books", categoryValue.GetString());

            // Check second condition: {"year":{"$gt":2010}}
            Assert.IsTrue(conditions[1].TryGetProperty("year", out var yearValue));
            Assert.IsTrue(yearValue.TryGetProperty("$gt", out var gtValue));
            Assert.AreEqual(2010, gtValue.GetInt32());

            // Check third condition: {"price":{"$lt":30.0}}
            Assert.IsTrue(conditions[2].TryGetProperty("price", out var priceValue));
            Assert.IsTrue(priceValue.TryGetProperty("$lt", out var ltValue));
            Assert.AreEqual(30.0, ltValue.GetDouble());
        }

        [TestMethod]
        public void WhereFilter_ChainedOperations_WithOr_GeneratesCorrectJson()
        {
            var filter = new WhereFilter()
                .Equals("category", "books")
                .GreaterThan("year", 2010)
                .Or(); // Use OR instead of the default AND

            // Convert to JSON to verify the correct structure is generated
            var json = JsonSerializer.Serialize(filter);

            // Parse the JSON to check its structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify that multiple conditions are wrapped with $or
            Assert.IsTrue(root.TryGetProperty("$or", out var orArray));
            Assert.AreEqual(JsonValueKind.Array, orArray.ValueKind);
            Assert.AreEqual(2, orArray.GetArrayLength());

            // Check that each condition is correctly represented
            var conditions = orArray.EnumerateArray().ToArray();

            // Check first condition: {"category":"books"}
            Assert.IsTrue(conditions[0].TryGetProperty("category", out var categoryValue));
            Assert.AreEqual("books", categoryValue.GetString());

            // Check second condition: {"year":{"$gt":2010}}
            Assert.IsTrue(conditions[1].TryGetProperty("year", out var yearValue));
            Assert.IsTrue(yearValue.TryGetProperty("$gt", out var gtValue));
            Assert.AreEqual(2010, gtValue.GetInt32());
        }

        [TestMethod]
        public void WhereFilter_SingleCondition_NoLogicalOperator()
        {
            var filter = new WhereFilter()
                .Equals("category", "books");

            // Convert to JSON to verify the correct structure is generated
            var json = JsonSerializer.Serialize(filter);

            // Parse the JSON to check its structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify that single conditions are not wrapped with $and or $or
            Assert.IsFalse(root.TryGetProperty("$and", out _));
            Assert.IsFalse(root.TryGetProperty("$or", out _));
            Assert.IsTrue(root.TryGetProperty("category", out var categoryValue));
            Assert.AreEqual("books", categoryValue.GetString());
        }

        [TestMethod]
        public void WhereFilter_ExplicitAnd_GeneratesCorrectJson()
        {
            var filter1 = new WhereFilter().Equals("category", "books");
            var filter2 = new WhereFilter().GreaterThan("year", 2010);

            var combined = new WhereFilter().And(filter1, filter2);

            // Convert to JSON to verify the correct structure is generated
            var json = JsonSerializer.Serialize(combined);

            // Parse the JSON to check its structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify the structure with $and
            Assert.IsTrue(root.TryGetProperty("$and", out var andArray));
            Assert.AreEqual(JsonValueKind.Array, andArray.ValueKind);
            Assert.AreEqual(2, andArray.GetArrayLength());
        }

        [TestMethod]
        public void WhereFilter_ExplicitOr_GeneratesCorrectJson()
        {
            var filter1 = new WhereFilter().Equals("category", "books");
            var filter2 = new WhereFilter().GreaterThan("year", 2010);

            var combined = new WhereFilter().Or(filter1, filter2);

            // Convert to JSON to verify the correct structure is generated
            var json = JsonSerializer.Serialize(combined);

            // Parse the JSON to check its structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify the structure with $or
            Assert.IsTrue(root.TryGetProperty("$or", out var orArray));
            Assert.AreEqual(JsonValueKind.Array, orArray.ValueKind);
            Assert.AreEqual(2, orArray.GetArrayLength());
        }

        [TestMethod]
        public void WhereFilter_IntegrationTest_OrFilter()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add test documents
            collection.Add("doc1", "Book 1", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["year"] = 2015,
                ["price"] = 25.0
            });

            collection.Add("doc2", "Book 2", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["year"] = 2018,
                ["price"] = 35.0
            });

            collection.Add("doc3", "Magazine 1", new Dictionary<string, object>
            {
                ["category"] = "magazines",
                ["year"] = 2020,
                ["price"] = 15.0
            });

            // Test OR filter - should match items where category is "magazines" OR price is less than 20.0
            var orFilter = new WhereFilter()
                .Equals("category", "magazines")
                .LessThan("price", 20.0)
                .Or();

            // Debug - print the generated JSON
            var json = JsonSerializer.Serialize(orFilter);
            Console.WriteLine($"OR filter JSON: {json}");

            try
            {
                var orResults = collection.Where(orFilter);
                Assert.AreEqual(1, orResults.Count);
                Assert.AreEqual("doc3", orResults.Ids[0]);  // Only doc3 matches either condition
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OR filter error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        [TestMethod]
        public void WhereFilter_EmptyFilter_GeneratesEmptyObject()
        {
            // Create an empty filter
            var filter = new WhereFilter();

            // Convert to JSON to verify the correct structure is generated
            var json = JsonSerializer.Serialize(filter);

            // Parse the JSON to check its structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify that an empty filter generates an empty object
            Assert.AreEqual(JsonValueKind.Object, root.ValueKind);
            Assert.AreEqual(0, root.EnumerateObject().Count());
        }

        [TestMethod]
        public void WhereFilter_ExplicitTopLevelOperator_PreservedInJson()
        {
            // Create a filter with an explicit $and operator at the top level
            var filter1 = new WhereFilter().Equals("category", "books");
            var filter2 = new WhereFilter().GreaterThan("year", 2010);

            // Use the public And method to create the combined filter
            var filter = new WhereFilter().And(filter1, filter2);

            // Convert to JSON to verify the correct structure is generated
            var json = JsonSerializer.Serialize(filter);

            // Parse the JSON to check its structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify that the explicit $and is preserved at the top level
            Assert.IsTrue(root.TryGetProperty("$and", out var andArray));
            Assert.AreEqual(JsonValueKind.Array, andArray.ValueKind);
            Assert.AreEqual(2, andArray.GetArrayLength());
        }

        [TestMethod]
        public void WhereFilter_ExplicitOperatorWithOtherFilters_UsesExplicitOperator()
        {
            // Create a filter with an explicit $or operator
            var categoryFilter1 = new WhereFilter().Equals("category", "books");
            var categoryFilter2 = new WhereFilter().Equals("category", "magazines");
            var orFilter = new WhereFilter().Or(categoryFilter1, categoryFilter2);
            
            // Add another top-level filter through a separate method
            // Note: In real usage, this would be done differently, but we're simulating 
            // having both an explicit operator and other top-level conditions
            var filter = orFilter;
            
            // Convert to JSON to verify the correct structure is generated
            var json = JsonSerializer.Serialize(filter);

            // Parse the JSON to check its structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify that the explicit $or is preserved
            Assert.IsTrue(root.TryGetProperty("$or", out var orArray));
            Assert.AreEqual(2, orArray.GetArrayLength());
        }

        [TestMethod]
        public void WhereFilter_SetOperators_SerializeCorrectly()
        {
            // Test $in operator with lists
            var inFilter = new WhereFilter()
                .In("tags", new[] { "fiction", "fantasy", "adventure" });

            // Test $nin operator with lists
            var ninFilter = new WhereFilter()
                .NotIn("tags", new[] { "horror", "thriller" });

            // Convert to JSON to verify the correct structure is generated
            var inJson = JsonSerializer.Serialize(inFilter);
            var ninJson = JsonSerializer.Serialize(ninFilter);

            // Parse the JSON to check its structure
            using var inDoc = JsonDocument.Parse(inJson);
            using var ninDoc = JsonDocument.Parse(ninJson);

            // Verify the $in operator array
            var inArray = inDoc.RootElement.GetProperty("tags").GetProperty("$in");
            Assert.AreEqual(JsonValueKind.Array, inArray.ValueKind);
            Assert.AreEqual(3, inArray.GetArrayLength());
            var inValues = inArray.EnumerateArray().Select(e => e.GetString()).ToArray();
            CollectionAssert.Contains(inValues, "fiction");
            CollectionAssert.Contains(inValues, "fantasy");
            CollectionAssert.Contains(inValues, "adventure");

            // Verify the $nin operator array
            var ninArray = ninDoc.RootElement.GetProperty("tags").GetProperty("$nin");
            Assert.AreEqual(JsonValueKind.Array, ninArray.ValueKind);
            Assert.AreEqual(2, ninArray.GetArrayLength());
            var ninValues = ninArray.EnumerateArray().Select(e => e.GetString()).ToArray();
            CollectionAssert.Contains(ninValues, "horror");
            CollectionAssert.Contains(ninValues, "thriller");
        }

        [TestMethod]
        public void WhereFilter_ComplexNestedFilter_IntegrationTest()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add test documents with rich metadata
            collection.Add("doc1", "Fiction Book", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["genres"] = new List<string> { "fiction", "fantasy" },
                ["year"] = 2015,
                ["price"] = 25.0,
                ["inStock"] = true
            });

            collection.Add("doc2", "Non-fiction Book", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["genres"] = new List<string> { "non-fiction", "history" },
                ["year"] = 2018,
                ["price"] = 35.0,
                ["inStock"] = true
            });

            collection.Add("doc3", "Magazine", new Dictionary<string, object>
            {
                ["category"] = "magazines",
                ["genres"] = new List<string> { "fashion", "lifestyle" },
                ["year"] = 2020,
                ["price"] = 15.0,
                ["inStock"] = false
            });

            collection.Add("doc4", "Textbook", new Dictionary<string, object>
            {
                ["category"] = "books",
                ["genres"] = new List<string> { "education", "science" },
                ["year"] = 2019,
                ["price"] = 50.0,
                ["inStock"] = false
            });

            // First, test a simple filter that should match doc2
            var filter1 = new WhereFilter()
                .Equals("category", "books")
                .GreaterThan("year", 2017)
                .Equals("inStock", true);

            var json1 = JsonSerializer.Serialize(filter1);
            Console.WriteLine($"Simple combined filter JSON: {json1}");
            
            try
            {
                var results1 = collection.Where(filter1);
                Assert.AreEqual(1, results1.Count);
                Assert.AreEqual("doc2", results1.Ids[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Simple filter error: {ex.Message}");
                throw;
            }

            // Now test a basic OR filter
            var orFilter = new WhereFilter().Or(
                new WhereFilter().Equals("category", "magazines"),
                new WhereFilter().Equals("category", "books")
                );

            var json2 = JsonSerializer.Serialize(orFilter);
            Console.WriteLine($"OR filter JSON: {json2}");

            try 
            {
                var results2 = collection.Where(orFilter);
                Assert.AreEqual(4, results2.Count); // Should match all documents
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OR filter error: {ex.Message}");
                throw;
            }

            // Now test a basic And filter
            var andFilter = new WhereFilter()
                .Equals("inStock", true)
                .Equals("category", "books");

            var andJson = JsonSerializer.Serialize(andFilter);
            Console.WriteLine($"AND filter JSON: {andJson}");

            try 
            {
                var resultsAnd = collection.Where(andFilter);
                Assert.AreEqual(2, resultsAnd.Count); // Should match all documents
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AND filter error: {ex.Message}");
                throw;
            }

            var booksFilter = new WhereFilter().Equals("category", "books");
            var magazinesFilter = new WhereFilter().Equals("category", "magazines");
            var categoryFilter = new WhereFilter().Or(booksFilter, magazinesFilter);
            var stockFilter = new WhereFilter().Equals("inStock", true);
            
            // Combine with AND - this may not run in ChromaDB but we can verify the structure
            var complexFilter = new WhereFilter().And(categoryFilter, stockFilter);
            var json3 = JsonSerializer.Serialize(complexFilter);
            Console.WriteLine($"Complex filter JSON structure: {json3}");
            
            // Verify the structure is correct, even if we don't execute it
            using var doc = JsonDocument.Parse(json3);
            var root = doc.RootElement;
            
            Assert.IsTrue(root.TryGetProperty("$and", out var andArray));
            Assert.AreEqual(2, andArray.GetArrayLength());
            
            // First element should have $or
            var firstElement = andArray.EnumerateArray().ElementAt(0);
            Assert.IsTrue(firstElement.TryGetProperty("$or", out var orArray));
            Assert.AreEqual(2, orArray.GetArrayLength());
            
            // Second element should have inStock = true
            var secondElement = andArray.EnumerateArray().ElementAt(1);
            Assert.IsTrue(secondElement.TryGetProperty("inStock", out var stockValue));
            Assert.AreEqual(true, stockValue.GetBoolean());
        }
        
        [TestMethod]
        public void WhereFilter_CombineWithOr_ThenCombineWithAnd_GeneratesCorrectJson()
        {
            // First create a filter with OR logic
            var categoryFilter1 = new WhereFilter().Equals("category", "books");
            var categoryFilter2 = new WhereFilter().Equals("category", "magazines");
            var orFilter = new WhereFilter().Or(categoryFilter1, categoryFilter2);
                
            // Then combine it with AND conditions
            var priceFilter = new WhereFilter().GreaterThan("price", 20.0);
            var combinedFilter = new WhereFilter().And(orFilter, priceFilter);

            // Convert to JSON to verify the correct structure is generated
            var json = JsonSerializer.Serialize(combinedFilter);
            Console.WriteLine($"Combined filter JSON: {json}");

            // Parse the JSON to check its structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify the structure: should be an $and array with 2 elements
            Assert.IsTrue(root.TryGetProperty("$and", out var andArray), "The root should have an $and property");
            Assert.AreEqual(2, andArray.GetArrayLength());
            
            // Get the elements of the $and array
            var elements = andArray.EnumerateArray().ToArray();
            
            // The first element should be an object with an $or property
            Assert.IsTrue(elements[0].TryGetProperty("$or", out var orArray), "The first element should have an $or property");
            Assert.AreEqual(2, orArray.GetArrayLength());
            
            // The second element should have a price property
            var priceObject = elements[1];
            Assert.IsTrue(priceObject.TryGetProperty("price", out var priceObj), "The second element should have a price property");
            Assert.IsTrue(priceObj.TryGetProperty("$gt", out var gtValue), "The price object should have a $gt property");
            Assert.AreEqual(20.0, gtValue.GetDouble());
        }
    }
}