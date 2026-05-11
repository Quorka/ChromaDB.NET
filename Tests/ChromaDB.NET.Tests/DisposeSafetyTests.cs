using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChromaDB.NET.Tests
{
    [TestClass]
    public class DisposeSafetyTests
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
        public void Client_UseAfterDispose_ThrowsObjectDisposedException()
        {
            var client = new ChromaClient(persistDirectory: _testDir);
            client.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => client.Heartbeat());
        }

        [TestMethod]
        public void Client_DoubleDispose_DoesNotThrow()
        {
            var client = new ChromaClient(persistDirectory: _testDir);
            client.Dispose();
            client.Dispose();
        }

        [TestMethod]
        public void Collection_UseAfterDispose_ThrowsObjectDisposedException()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);
            collection.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => collection.Count());
        }

        [TestMethod]
        public void Collection_DoubleDispose_DoesNotThrow()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);
            collection.Dispose();
            collection.Dispose();
        }

        [TestMethod]
        public void Collection_UseAfterClientDispose_ThrowsObjectDisposedException()
        {
            var client = new ChromaClient(persistDirectory: _testDir);
            var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);
            client.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => collection.Count());
            collection.Dispose();
        }

        [TestMethod]
        public void Client_CreateCollection_AfterDispose_ThrowsObjectDisposedException()
        {
            var client = new ChromaClient(persistDirectory: _testDir);
            client.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(
                () => client.CreateCollection("test", _embeddingFunction));
        }

        [TestMethod]
        public void Client_GetCollection_AfterDispose_ThrowsObjectDisposedException()
        {
            var client = new ChromaClient(persistDirectory: _testDir);
            client.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(
                () => client.GetCollection("test", _embeddingFunction));
        }

        [TestMethod]
        public void Client_DatabaseOps_AfterDispose_ThrowsObjectDisposedException()
        {
            var client = new ChromaClient(persistDirectory: _testDir);
            client.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(
                () => client.CreateDatabase("test-db"));
            Assert.ThrowsException<ObjectDisposedException>(
                () => client.GetDatabaseId("test-db"));
            Assert.ThrowsException<ObjectDisposedException>(
                () => client.DeleteDatabase("test-db"));
        }

        [TestMethod]
        public void Collection_AllOps_AfterDispose_ThrowObjectDisposedException()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);
            collection.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => collection.Count());
            Assert.ThrowsException<ObjectDisposedException>(
                () => collection.Add("id", "text"));
            Assert.ThrowsException<ObjectDisposedException>(
                () => collection.Query("text"));
            Assert.ThrowsException<ObjectDisposedException>(
                () => collection.Get());
            Assert.ThrowsException<ObjectDisposedException>(
                () => collection.Delete("id"));
            Assert.ThrowsException<ObjectDisposedException>(
                () => collection.Update("id", "text"));
            Assert.ThrowsException<ObjectDisposedException>(
                () => collection.Upsert("id", "text"));
            Assert.ThrowsException<ObjectDisposedException>(
                () => collection.Where(new WhereFilter().Equals("k", "v")));
            Assert.ThrowsException<ObjectDisposedException>(
                () => collection.Search("text"));
        }

        [TestMethod]
        public void ConcurrentOperations_DoNotCrash()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: _embeddingFunction);

            collection.Add("seed", "seed document",
                new System.Collections.Generic.Dictionary<string, object> { ["k"] = "v" });

            var tasks = new Task[10];
            var errors = new Exception[10];

            for (int i = 0; i < tasks.Length; i++)
            {
                int idx = i;
                tasks[i] = Task.Run(() =>
                {
                    try
                    {
                        collection.Count();
                        collection.Get();
                        collection.Query(queryText: "seed", nResults: 1);
                    }
                    catch (Exception ex)
                    {
                        errors[idx] = ex;
                    }
                });
            }

            if (!Task.WaitAll(tasks, TimeSpan.FromSeconds(30)))
                Assert.Fail("Concurrent operations timed out after 30 seconds");

            for (int i = 0; i < errors.Length; i++)
            {
                if (errors[i] != null)
                    Assert.Fail($"Task {i} threw: {errors[i]}");
            }
        }
    }
}
