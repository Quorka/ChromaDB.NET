using System;
using System.Collections.Generic;

namespace ChromaDB.NET.Tests
{
    /// <summary>
    /// Extension methods for ChromaClient to assist with testing
    /// </summary>
    public static class ChromaClientTestExtensions
    {
        /// <summary>
        /// Creates a collection with a unique name using a GUID
        /// </summary>
        /// <param name="client">The ChromaClient instance</param>
        /// <param name="baseName">Optional base name prefix</param>
        /// <param name="embeddingFunction">Optional embedding function</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>A collection with a unique GUID-based name</returns>
        public static Collection CreateCollectionWithUniqueName(
            this ChromaClient client,
            string baseName = "test-collection",
            IEmbeddingFunction embeddingFunction = null,
            Dictionary<string, object> metadata = null)
        {
            string uniqueName = $"{baseName}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            return client.CreateCollection(uniqueName, embeddingFunction, metadata);
        }
    }
}