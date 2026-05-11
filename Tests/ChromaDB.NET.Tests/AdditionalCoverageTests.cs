using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChromaDB.NET.Tests
{
    [TestClass]
    public class AdditionalCoverageTests
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
                    Thread.Sleep(100);
                    Directory.Delete(_testDir, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up test directory '{_testDir}': {ex.Message}");
            }
        }

        [TestMethod]
        public void Heartbeat_ReturnsNonZero()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            var heartbeat = client.Heartbeat();
            Assert.IsTrue(heartbeat > 0, "Heartbeat should return a positive timestamp");
        }

        [TestMethod]
        public void GetOrCreateCollection_NotFoundFallsBackToCreate()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            string name = $"new-collection-{Guid.NewGuid():N}".Substring(0, 30);

            using var collection = client.GetOrCreateCollection(name, _embeddingFunction);
            Assert.IsNotNull(collection);

            collection.Add("doc1", "test", new Dictionary<string, object> { ["k"] = "v" });
            Assert.AreEqual<uint>(1, collection.Count());
        }

        [TestMethod]
        public void GetOrCreateCollection_ExistingCollection_ReturnsExisting()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            string name = $"existing-col-{Guid.NewGuid():N}".Substring(0, 30);

            using var created = client.CreateCollection(name, _embeddingFunction);
            created.Add("doc1", "test", new Dictionary<string, object> { ["k"] = "v" });

            using var fetched = client.GetOrCreateCollection(name, _embeddingFunction);
            Assert.AreEqual<uint>(1, fetched.Count());
        }

        [TestMethod]
        public void BatchAdd_100Documents_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            var docs = new List<ChromaDocument>();
            for (int i = 0; i < 100; i++)
            {
                docs.Add(ChromaDocument.Create(
                    $"doc-{i}",
                    $"Document number {i} with some text content",
                    new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["batch"] = "test"
                    }));
            }

            collection.Add(docs);
            Assert.AreEqual<uint>(100, collection.Count());

            var results = collection.Get(limit: 10, includeDocuments: true);
            Assert.AreEqual(10, results.Ids.Count);
        }

        [TestMethod]
        public void BatchAdd_ThenQuery_DistancesOrdered()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: new TestEmbeddingFunction(dimension: 8));

            for (int i = 0; i < 20; i++)
            {
                collection.Add($"doc-{i}", $"Document {i}",
                    new Dictionary<string, object> { ["i"] = i });
            }

            var results = collection.Query(queryText: "Document 0", nResults: 5, includeDistances: true);

            Assert.AreEqual(5, results.Ids.Count);
            Assert.AreEqual(5, results.Distances.Count);

            for (int i = 1; i < results.Distances.Count; i++)
            {
                Assert.IsTrue(results.Distances[i] >= results.Distances[i - 1],
                    $"Distances should be non-decreasing: [{i-1}]={results.Distances[i-1]} > [{i}]={results.Distances[i]}");
            }
        }

        [TestMethod]
        public void Update_NonExistentDocument_DoesNotThrow()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            collection.Add("existing", "some text",
                new Dictionary<string, object> { ["k"] = "v" });

            // ChromaDB silently ignores updates for non-existent IDs
            collection.Update("does-not-exist", "update attempt");

            Assert.AreEqual<uint>(1, collection.Count());
            var doc = collection.GetById("existing");
            Assert.AreEqual("some text", doc.Text);
        }

        [TestMethod]
        public void WhereFilter_NotEquals_IntegrationTest()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            collection.Add("doc1", "Cat", new Dictionary<string, object> { ["animal"] = "cat" });
            collection.Add("doc2", "Dog", new Dictionary<string, object> { ["animal"] = "dog" });
            collection.Add("doc3", "Bird", new Dictionary<string, object> { ["animal"] = "bird" });

            var filter = new Dictionary<string, object>
            {
                ["animal"] = new Dictionary<string, object> { ["$ne"] = "cat" }
            };

            var results = collection.Get(whereFilter: filter);
            Assert.AreEqual(2, results.Ids.Count);
            Assert.IsFalse(results.Ids.Contains("doc1"));
            Assert.IsTrue(results.Ids.Contains("doc2"));
            Assert.IsTrue(results.Ids.Contains("doc3"));
        }

        [TestMethod]
        public void Query_WithWhereDocumentFilter()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            collection.Add("doc1", "The quick brown fox jumps over the lazy dog",
                new Dictionary<string, object> { ["source"] = "test" });
            collection.Add("doc2", "Artificial intelligence is transforming the world",
                new Dictionary<string, object> { ["source"] = "test" });
            collection.Add("doc3", "The fox ran across the field",
                new Dictionary<string, object> { ["source"] = "test" });

            var results = collection.Query(
                queryText: "fox",
                nResults: 10,
                whereDocument: "{\"$contains\": \"fox\"}",
                includeDocuments: true);

            Assert.IsTrue(results.Ids.Count >= 1);
            foreach (var doc in results.Documents)
            {
                Assert.IsTrue(doc.Contains("fox", StringComparison.OrdinalIgnoreCase),
                    $"Document should contain 'fox': {doc}");
            }
        }

        [TestMethod]
        public void Get_WithPagination_ReturnsCorrectSubset()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            for (int i = 0; i < 10; i++)
            {
                collection.Add($"doc-{i:D2}", $"Document {i}",
                    new Dictionary<string, object> { ["i"] = i });
            }

            var page1 = collection.Get(limit: 3, offset: 0);
            var page2 = collection.Get(limit: 3, offset: 3);

            Assert.AreEqual(3, page1.Ids.Count);
            Assert.AreEqual(3, page2.Ids.Count);

            var allIds = page1.Ids.Concat(page2.Ids).ToList();
            Assert.AreEqual(6, allIds.Distinct().Count(), "Pages should not overlap");
        }

        [TestMethod]
        public void QueryResult_Enumerable_Works()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            collection.Add("doc1", "First",
                new Dictionary<string, object> { ["order"] = 1 });
            collection.Add("doc2", "Second",
                new Dictionary<string, object> { ["order"] = 2 });

            var results = collection.Get();

            int count = 0;
            foreach (var doc in results)
            {
                Assert.IsNotNull(doc.Id);
                Assert.IsNotNull(doc.Text);
                count++;
            }
            Assert.AreEqual(2, count);

            var ids = results.Select(d => d.Id).ToList();
            Assert.AreEqual(2, ids.Count);
        }

        [TestMethod]
        public void EmptyCollection_Query_ReturnsEmptyResults()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            Assert.AreEqual<uint>(0, collection.Count());

            var results = collection.Query(queryText: "anything", nResults: 5);
            Assert.AreEqual(0, results.Ids.Count);
        }

        [TestMethod]
        public void Delete_WithWhereFilter_DeletesMatchingDocuments()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            collection.Add("doc1", "Keep this",
                new Dictionary<string, object> { ["status"] = "keep" });
            collection.Add("doc2", "Delete this",
                new Dictionary<string, object> { ["status"] = "delete" });
            collection.Add("doc3", "Also delete",
                new Dictionary<string, object> { ["status"] = "delete" });

            Assert.AreEqual<uint>(3, collection.Count());

            collection.Delete(
                whereFilter: new Dictionary<string, object> { ["status"] = "delete" });

            Assert.AreEqual<uint>(1, collection.Count());
            var remaining = collection.Get();
            Assert.AreEqual("doc1", remaining.Ids[0]);
        }

        [TestMethod]
        public void Add_EmptyList_DoesNotThrow()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            collection.Add(new List<ChromaDocument>());
            Assert.AreEqual<uint>(0, collection.Count());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Delete_NoArguments_ThrowsArgumentException()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            collection.Delete();
        }

        [TestMethod]
        public void GetById_NonExistent_ReturnsNull()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            var doc = collection.GetById("does-not-exist");
            Assert.IsNull(doc);
        }

        [TestMethod]
        public void Delete_NonExistentId_DoesNotThrow()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            collection.Add("doc1", "exists",
                new Dictionary<string, object> { ["k"] = "v" });

            collection.Delete("does-not-exist");

            Assert.AreEqual<uint>(1, collection.Count());
        }
    }
}
