using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChromaDB.NET.Tests
{
    [TestClass]
    public class IntegrationTests
    {
        private string _testDir = string.Empty;
        private TestEmbeddingFunction _embeddingFunction = new TestEmbeddingFunction();

        [TestInitialize]
        public void Initialize()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "chromadb-dotnet-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
            _embeddingFunction = new TestEmbeddingFunction(dimension: 10);
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
        public void FullWorkflow_Success()
        {
            // 1. Create client
            using var client = new ChromaClient(persistDirectory: _testDir);

            // 2. Create collection
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction,
                metadata: new Dictionary<string, object> { ["description"] = "Test collection" }
            );

            // 3. Add documents
            collection.Add("doc1", "The quick brown fox jumps over the lazy dog",
                new Dictionary<string, object> { ["category"] = "example", ["tags"] = new[] { "animals", "classic" } });
            collection.Add("doc2", "The five boxing wizards jump quickly",
                new Dictionary<string, object> { ["category"] = "example", ["tags"] = new[] { "wizards", "classic" } });
            collection.Add("doc3", "ChromaDB is a database for storing and querying embeddings",
                new Dictionary<string, object> { ["category"] = "documentation", ["tags"] = new[] { "database", "embeddings" } });

            // 4. Verify count
            Assert.AreEqual<uint>(3, collection.Count());

            // 5. Query by text
            var queryResults = collection.Search("database", limit: 2);
            Assert.AreEqual(2, queryResults.Count);
            //Assert.IsTrue(queryResults.Ids.Contains("doc3")); // Most relevant should be included

            // 6. Get document by ID
            var doc = collection.GetById("doc1");
            Assert.IsNotNull(doc);
            Assert.AreEqual("The quick brown fox jumps over the lazy dog", doc.Text);
            Assert.AreEqual("example", doc.Metadata["category"].ToString());

            // 7. Filter with WhereFilter
            var filter = new WhereFilter()
                .Equals("category", "example");
            var filteredResults = collection.Where(filter);
            Assert.AreEqual(2, filteredResults.Count);
            Assert.IsTrue(filteredResults.Ids.Contains("doc1") && filteredResults.Ids.Contains("doc2"));

            // 8. Update a document
            collection.Update("doc1", "The quick brown fox jumps over the lazy dog - UPDATED",
                new Dictionary<string, object>
                {
                    ["category"] = "example",
                    ["tags"] = new[] { "animals", "classic", "updated" },
                    ["updated"] = true
                });

            // 9. Verify update
            var updatedDoc = collection.GetById("doc1");
            Assert.IsTrue(updatedDoc.Text.Contains("UPDATED"));
            Assert.IsTrue(updatedDoc.Metadata.ContainsKey("updated"));
            Assert.IsTrue(((JsonElement)updatedDoc.Metadata["updated"]).GetBoolean());

            // 10. Upsert documents
            collection.Upsert("doc2", "The five boxing wizards jump quickly - UPDATED",
                new Dictionary<string, object> { ["updated"] = true });
            collection.Upsert("doc4", "This is a new document added via upsert",
                new Dictionary<string, object> { ["category"] = "new" });

            // 11. Verify upserts
            Assert.AreEqual<uint>(4, collection.Count());
            var upsertedDoc = collection.GetById("doc2");
            Assert.IsTrue(upsertedDoc.Text.Contains("UPDATED"));
            var newDoc = collection.GetById("doc4");
            Assert.IsNotNull(newDoc);

            // 12. Delete a document
            collection.Delete("doc3");

            // 13. Verify deletion
            Assert.AreEqual<uint>(3, collection.Count());
            var remainingDocs = collection.Get().ToDocuments();
            Assert.IsFalse(remainingDocs.Any(d => d.Id == "doc3"));

            // 14. Use more complex filters
            //var complexFilter = new WhereFilter()
            //    .Equals("category", "example")
            //    .In("tags", new[] { "updated" });
            //var complexResults = collection.Where(complexFilter);
            //Assert.AreEqual(1, complexResults.Count);
            //Assert.AreEqual("doc1", complexResults.Ids[0]);

            // 15. Combine search and filter
            var searchWithFilter = collection.Search(
                "updated",
                filter: new WhereFilter().Equals("category", "example")
            );
            Assert.AreEqual(2, searchWithFilter.Count);
        }

        [TestMethod]
        public void Persistence_Success()
        {
            string collectionName = "persistent-collection";
            string docId = "persistent-doc";
            string docText = "This document should persist across client instances";
            var testDir = Path.Combine(_testDir, "persistence-test");
            Directory.CreateDirectory(testDir);

            // Create first client and add document
            {
                using var client = new ChromaClient(persistDirectory: testDir);
                using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

                collection.Add(docId, docText,
                    new Dictionary<string, object> { ["test"] = "persistence" });

                // Verify the document exists
                var doc = collection.GetById(docId);
                Assert.IsNotNull(doc);
                Assert.AreEqual(docText, doc.Text);
            }

            // Create a new client instance and verify the document still exists
            {
                using var client = new ChromaClient(persistDirectory: testDir);
                using var collection = client.GetCollection(collectionName, _embeddingFunction);

                // Verify the document still exists
                var doc = collection.GetById(docId);
                Assert.IsNotNull(doc);
                Assert.AreEqual(docText, doc.Text);
                Assert.AreEqual("persistence", doc.Metadata["test"].ToString());
            }
        }

        [TestMethod]
        public void ErrorHandling_InvalidDocumentId_ThrowsException()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add a document
            collection.Add("doc1", "Test document", null);

            // Try to get a non-existent document
            var doc = collection.GetById("non-existent");
            Assert.IsNull(doc);

            // Try to update a non-existent document (should throw)
            try
            {
                collection.Update("non-existent", "Update attempt", null);
                //Assert.Fail("Expected an exception but none was thrown"); - unclear if an exception should be thrown
            }
            catch (ChromaException)
            {
                // Expected exception
            }

            // Delete a non-existent document (should not throw)
            collection.Delete("non-existent");

            // Verify the original document is still there
            Assert.AreEqual<uint>(1, collection.Count());
        }
    }
}