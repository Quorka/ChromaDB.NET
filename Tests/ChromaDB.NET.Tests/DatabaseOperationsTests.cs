using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChromaDB.NET.Tests
{
    [TestClass]
    public class DatabaseOperationsTests
    {
        private string _testDir = string.Empty;

        [TestInitialize]
        public void Initialize()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "chromadb-dotnet-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [TestMethod]
        public void CreateDatabase_Success()
        {
            var databaseName = "test-database-1";
            using var client = new ChromaClient(persistDirectory: _testDir);

            // Create a database
            client.CreateDatabase(databaseName);

            // If no exception is thrown, the test passes
        }

        [TestMethod]
        public void GetDatabaseId_Success()
        {
            var databaseName = "test-database-2";
            using var client = new ChromaClient(persistDirectory: _testDir);

            // Create a database
            client.CreateDatabase(databaseName);

            // Get the database ID
            var id = client.GetDatabaseId(databaseName);

            // Verify the ID is not null or empty
            Assert.IsFalse(string.IsNullOrEmpty(id));
        }

        [TestMethod]
        public void DeleteDatabase_Success()
        {
            var databaseName = "test-database-3";
            using var client = new ChromaClient(persistDirectory: _testDir);

            // Create a database
            client.CreateDatabase(databaseName);

            // Get the database ID to verify it exists
            var id = client.GetDatabaseId(databaseName);
            Assert.IsFalse(string.IsNullOrEmpty(id));

            // Delete the database
            client.DeleteDatabase(databaseName);

            // Try to get the database ID again, should throw an exception
            try
            {
                var id2 = client.GetDatabaseId(databaseName);
                Assert.Fail("Expected an exception but none was thrown.");
            }
            catch (ChromaException)
            {
                // Expected exception
            }
        }

    }
}