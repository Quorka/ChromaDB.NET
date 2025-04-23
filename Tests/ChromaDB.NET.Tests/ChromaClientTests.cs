using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChromaDB.NET.Tests
{
    // Simple embedding function for testing
    public class TestEmbeddingFunction : IEmbeddingFunction
    {
        private readonly int _dimensions;
        private readonly Random _random;

        public TestEmbeddingFunction()
        {
            _dimensions = 3;
            _random = new Random(42); // Fixed seed for reproducibility
        }

        public TestEmbeddingFunction(int dimension)
        {
            _dimensions = dimension;
            _random = new Random(42); // Fixed seed for reproducibility
        }

        public float[][] GenerateEmbeddings(IEnumerable<string> documents)
        {
            return documents.Select(_ => Enumerable.Range(0, _dimensions)
                .Select(__ => (float)_random.NextDouble())
                .ToArray())
                .ToArray();
        }

        public object Configuration => new { model_name = "test_embeddings" };
    }

    [TestClass]
    public class ChromaClientTests
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
                // Optionally re-throw or Assert.Fail if cleanup failure should fail the test run
                // Assert.Fail($"Cleanup failed: {ex.Message}");
            }
        }

        [TestMethod]
        public void CreateClient_Success()
        {
            using var client = new ChromaClient();
            Assert.IsNotNull(client);
        }

        [TestMethod]
        public void CreateCollection_Success()
        {
            using var client = new ChromaClient();
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);
            Assert.IsNotNull(collection);
        }

        [TestMethod]
        public void AddDocuments_Success()
        {
            using var client = new ChromaClient();
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            var documents = new List<ChromaDocument>
            {
                new ChromaDocument
                {
                    Id = "doc1",
                    Text = "Test document 1",
                    Metadata = new Dictionary<string, object> { { "source", "test" } }
                },
                new ChromaDocument
                {
                    Id = "doc2",
                    Text = "Test document 2",
                    Metadata = new Dictionary<string, object> { { "source", "test" } }
                }
            };

            collection.Add(documents);

            // The test passes if no exception is thrown
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void Query_ReturnsResults()
        {
            using var client = new ChromaClient();
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            var documents = new List<ChromaDocument>
            {
                new ChromaDocument
                {
                    Id = "doc1",
                    Text = "The quick brown fox jumps over the lazy dog",
                    Metadata = new Dictionary<string, object> { { "source", "test" } }
                },
                new ChromaDocument
                {
                    Id = "doc2",
                    Text = "The dog barked at the mailman",
                    Metadata = new Dictionary<string, object> { { "source", "test" } }
                }
            };

            collection.Add(documents);

            var results = collection.Query(
                queryText: "dog",
                nResults: 10,
                includeMetadatas: true,
                includeDocuments: true
            );

            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Ids.Count);
            Assert.IsTrue(results.Ids.Contains("doc1"));
            Assert.IsTrue(results.Ids.Contains("doc2"));
            Assert.AreEqual(2, results.Documents.Count);
            Assert.AreEqual(2, results.Metadatas.Count);
        }

        [TestMethod]
        public void Query_WithFilter_ReturnsFilteredResults()
        {
            using var client = new ChromaClient();
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            var documents = new List<ChromaDocument>
            {
                new ChromaDocument
                {
                    Id = "doc1",
                    Text = "The quick brown fox jumps over the lazy dog",
                    Metadata = new Dictionary<string, object> { { "category", "animals" } }
                },
                new ChromaDocument
                {
                    Id = "doc2",
                    Text = "The dog barked at the mailman",
                    Metadata = new Dictionary<string, object> { { "category", "animals" } }
                },
                new ChromaDocument
                {
                    Id = "doc3",
                    Text = "A rainbow appears after the rain",
                    Metadata = new Dictionary<string, object> { { "category", "weather" } }
                }
            };

            collection.Add(documents);

            var results = collection.Query(
                queryText: "nature",
                nResults: 10,
                whereFilter: new Dictionary<string, object> { { "category", "animals" } },
                includeMetadatas: true,
                includeDocuments: true
            );

            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Ids.Count);
            Assert.IsTrue(results.Ids.Contains("doc1"));
            Assert.IsTrue(results.Ids.Contains("doc2"));
            Assert.IsFalse(results.Ids.Contains("doc3"));
        }
    }
}