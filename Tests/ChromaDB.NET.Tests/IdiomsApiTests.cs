using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChromaDB.NET.Tests
{
    [TestClass]
    public class IdiomsApiTests
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
        public void ChromaDocument_CreateMethods_Success()
        {
            // Test the static Create method
            var doc1 = ChromaDocument.Create("doc1", "This is a test document",
                new Dictionary<string, object> { ["source"] = "test" });

            Assert.AreEqual("doc1", doc1.Id);
            Assert.AreEqual("This is a test document", doc1.Text);
            Assert.AreEqual("test", doc1.Metadata["source"]);
            Assert.IsNull(doc1.Embedding);

            // Test the CreateWithEmbedding method
            var embedding = new float[] { 0.1f, 0.2f, 0.3f };
            var doc2 = ChromaDocument.CreateWithEmbedding("doc2", embedding,
                new Dictionary<string, object> { ["source"] = "test" });

            Assert.AreEqual("doc2", doc2.Id);
            Assert.IsNull(doc2.Text);
            Assert.AreEqual("test", doc2.Metadata["source"]);
            Assert.AreEqual(embedding, doc2.Embedding);
        }

        [TestMethod]
        public void GetOrCreateCollection_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);

            // First create a new collection with unique name
            string collectionName1 = $"test-collection-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            using var collection1 = client.GetOrCreateCollection(collectionName1, _embeddingFunction);
            Assert.IsNotNull(collection1);

            // Now get the existing collection
            using var collection2 = client.GetOrCreateCollection(collectionName1, _embeddingFunction);
            Assert.IsNotNull(collection2);

            // Both operations should succeed without throwing exceptions
        }

        [TestMethod]
        public void Collection_SimpleAdd_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Test the simplified Add method
            collection.Add("doc1", "This is a test document",
                new Dictionary<string, object> { ["source"] = "test" });

            // Verify the document was added
            var results = collection.Get(ids: new[] { "doc1" });
            Assert.AreEqual(1, results.Ids.Count);
            Assert.AreEqual("doc1", results.Ids[0]);
            Assert.AreEqual("This is a test document", results.Documents[0]);
        }

        [TestMethod]
        public void Collection_SimpleUpdate_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add a document
            collection.Add("doc1", "This is a test document",
                new Dictionary<string, object> { ["source"] = "test", ["version"] = 1 });

            // Update the document
            collection.Update("doc1", "This is an updated document",
                new Dictionary<string, object> { ["source"] = "test", ["version"] = 2 });

            // Verify the document was updated
            var results = collection.Get(ids: new[] { "doc1" });
            Assert.AreEqual(1, results.Ids.Count);
            Assert.AreEqual("doc1", results.Ids[0]);
            Assert.AreEqual("This is an updated document", results.Documents[0]);
            Assert.AreEqual("2", results.Metadatas[0]["version"].ToString());
        }

        [TestMethod]
        public void Collection_SimpleUpsert_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add a document
            collection.Add("doc1", "This is a test document",
                new Dictionary<string, object> { ["source"] = "test", ["version"] = 1 });

            // Upsert an existing document
            collection.Upsert("doc1", "This is an upserted document",
                new Dictionary<string, object> { ["source"] = "test", ["version"] = 2 });

            // Upsert a new document
            collection.Upsert("doc2", "This is a new document",
                new Dictionary<string, object> { ["source"] = "test", ["version"] = 1 });

            // Verify document 1 was updated
            var doc1 = collection.GetById("doc1");
            Assert.AreEqual("This is an upserted document", doc1.Text);
            Assert.AreEqual(2, ((JsonElement)doc1.Metadata["version"]).GetInt32());

            // Verify document 2 was added
            var doc2 = collection.GetById("doc2");
            Assert.AreEqual("This is a new document", doc2.Text);
            Assert.AreEqual(1, ((JsonElement)doc2.Metadata["version"]).GetInt32());
        }

        [TestMethod]
        public void Collection_GetById_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add a document
            collection.Add("doc1", "This is a test document",
                new Dictionary<string, object> { ["source"] = "test" });

            // Get the document by ID
            var doc = collection.GetById("doc1");

            // Verify the document
            Assert.IsNotNull(doc);
            Assert.AreEqual("doc1", doc.Id);
            Assert.AreEqual("This is a test document", doc.Text);
            Assert.AreEqual("test", ((JsonElement)doc.Metadata["source"]).GetString());
        }

        [TestMethod]
        public void Collection_Delete_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add documents
            collection.Add("doc1", "Document 1", new Dictionary<string, object> { ["source"] = "test" });
            collection.Add("doc2", "Document 2", new Dictionary<string, object> { ["source"] = "test" });

            // Verify both documents exist
            Assert.AreEqual<uint>(2, collection.Count());

            // Delete one document
            collection.Delete("doc1");

            // Verify only one document remains
            Assert.AreEqual<uint>(1, collection.Count());

            // Check if the right document was deleted
            var doc = collection.GetById("doc2");
            Assert.IsNotNull(doc);
        }

        [TestMethod]
        public void Collection_Count_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Initially empty
            Assert.AreEqual<uint>(0, collection.Count());

            // Add one document
            collection.Add("doc1", "Document 1", new Dictionary<string, object> { ["source"] = "test" });
            Assert.AreEqual<uint>(1, collection.Count());

            // Add another document
            collection.Add("doc2", "Document 2", new Dictionary<string, object> { ["source"] = "test" });
            Assert.AreEqual<uint>(2, collection.Count());

            // Delete a document
            collection.Delete("doc1");
            Assert.AreEqual<uint>(1, collection.Count());
        }

        [TestMethod]
        public void Collection_Search_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add documents
            collection.Add("doc1", "The quick brown fox jumps over the lazy dog",
                new Dictionary<string, object> { ["category"] = "animals" });
            collection.Add("doc2", "A rainbow appears after the rain",
                new Dictionary<string, object> { ["category"] = "weather" });

            // Search for documents
            var results = collection.Search("fox", limit: 1);

            // Verify results
            Assert.AreEqual(1, results.Ids.Count);
            Assert.AreEqual("doc1", results.Ids[0]);
        }

        [TestMethod]
        public void Collection_Search_WithFilter_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add documents
            collection.Add("doc1", "The quick brown fox jumps over the lazy dog",
                new Dictionary<string, object> { ["category"] = "animals" });
            collection.Add("doc2", "Dogs and cats are common pets",
                new Dictionary<string, object> { ["category"] = "animals" });
            collection.Add("doc3", "A rainbow appears after the rain",
                new Dictionary<string, object> { ["category"] = "weather" });

            // Search with filter
            var results = collection.Search(
                "animals",
                filter: new WhereFilter().Equals("category", "animals"),
                limit: 2
            );

            // Verify results
            Assert.AreEqual(2, results.Ids.Count);
            Assert.IsTrue(results.Ids.Contains("doc1"));
            Assert.IsTrue(results.Ids.Contains("doc2"));
            Assert.IsFalse(results.Ids.Contains("doc3"));
        }

        [TestMethod]
        public void QueryResult_ToDocuments_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add documents
            collection.Add("doc1", "The quick brown fox",
                new Dictionary<string, object> { ["category"] = "animals" });
            collection.Add("doc2", "The lazy dog",
                new Dictionary<string, object> { ["category"] = "animals" });

            // Get documents
            var results = collection.Get(ids: new[] { "doc1", "doc2" });

            // Convert to documents
            var documents = results.ToDocuments();

            // Verify conversion
            Assert.AreEqual(2, documents.Count);
            Assert.AreEqual("doc1", documents[0].Id);
            Assert.AreEqual("The quick brown fox", documents[0].Text);
            Assert.AreEqual("animals", ((JsonElement)documents[0].Metadata["category"]).GetString());
            Assert.AreEqual("doc2", documents[1].Id);
        }

        [TestMethod]
        public void QueryResult_FirstOrDefault_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            // Add documents
            collection.Add("doc1", "The quick brown fox",
                new Dictionary<string, object> { ["category"] = "animals" });

            // Get documents
            var results = collection.Get(ids: new[] { "doc1" });

            // Get first document
            var doc = results.FirstOrDefault();

            // Verify document
            Assert.IsNotNull(doc);
            Assert.AreEqual("doc1", doc.Id);
            Assert.AreEqual("The quick brown fox", doc.Text);
            Assert.AreEqual("animals", ((JsonElement)doc.Metadata["category"]).GetString());

            // Test with empty results
            var emptyResults = collection.Get(ids: new[] { "non-existent" });
            var emptyDoc = emptyResults.FirstOrDefault();
            Assert.IsNull(emptyDoc);
        }
    }
}