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

            // Test combined filters
            //var filter4 = new WhereFilter()
            //    .Equals("category", "books")
            //    .GreaterThan("year", 2015)
            //    .LessThan("price", 40.0);
            //var results4 = collection.Where(filter4);
            //Assert.AreEqual(1, results4.Count);
            //Assert.AreEqual("doc2", results4.Ids[0]);

            // Test In operator
            var filter5 = new WhereFilter().In("category", new[] { "books", "newspapers" });
            var results5 = collection.Where(filter5);
            Assert.AreEqual(2, results5.Count);
            Assert.IsTrue(results5.Ids.Contains("doc1") && results5.Ids.Contains("doc2"));
        }
    }
}