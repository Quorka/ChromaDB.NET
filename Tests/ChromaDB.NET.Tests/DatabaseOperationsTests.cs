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
            using var client = new ChromaClient(persistDirectory: _testDir);
            
            // Create a database
            client.CreateDatabase("test-database");
            
            // If no exception is thrown, the test passes
        }
        
        [TestMethod]
        public void GetDatabaseId_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            
            // Create a database
            client.CreateDatabase("test-database");
            
            // Get the database ID
            var id = client.GetDatabaseId("test-database");
            
            // Verify the ID is not null or empty
            Assert.IsFalse(string.IsNullOrEmpty(id));
        }
        
        [TestMethod]
        public void DeleteDatabase_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            
            // Create a database
            client.CreateDatabase("test-database");
            
            // Get the database ID to verify it exists
            var id = client.GetDatabaseId("test-database");
            Assert.IsFalse(string.IsNullOrEmpty(id));
            
            // Delete the database
            client.DeleteDatabase("test-database");
            
            // Try to get the database ID again, should throw an exception
            try
            {
                var id2 = client.GetDatabaseId("test-database");
                Assert.Fail("Expected an exception but none was thrown.");
            }
            catch (ChromaException)
            {
                // Expected exception
            }
        }
        
        // Note: We'll use the high-level API for testing database operations
        // Since the NativeMethods are internal and not accessible from test project
        [TestMethod]
        [Ignore("Database specific collection operations not fully supported in the high-level API yet")]
        public void CreateCollection_InDatabase_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            
            // Create a database
            client.CreateDatabase("test-database");
            
            // Since we can't directly access the internal API, we'll just document
            // that this test should be implemented when the API supports database-specific
            // collection operations
            Assert.Inconclusive("Test requires implementation of database-specific collection operations in the high-level API");
        }
        
        [TestMethod]
        [Ignore("Database specific collection operations not fully supported in the high-level API yet")]
        public void GetCollection_FromDatabase_Success()
        {
            using var client = new ChromaClient(persistDirectory: _testDir);
            
            // Create a database
            client.CreateDatabase("test-database");
            
            // Since we can't directly access the internal API, we'll just document
            // that this test should be implemented when the API supports database-specific
            // collection operations
            Assert.Inconclusive("Test requires implementation of database-specific collection operations in the high-level API");
        }
    }
}