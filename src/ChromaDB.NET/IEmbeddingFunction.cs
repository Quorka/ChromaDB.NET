using System;
using System.Collections.Generic;
namespace ChromaDB.NET;

/// <summary>
/// Interface for embedding functions
/// </summary>
public interface IEmbeddingFunction
{
    /// <summary>
    /// Generate embeddings for a list of documents
    /// </summary>
    /// <param name="documents">List of document texts</param>
    /// <returns>Array of embedding vectors</returns>
    float[][] GenerateEmbeddings(IEnumerable<string> documents);

    /// <summary>
    /// Configuration details for serialization
    /// </summary>
    object Configuration { get; }
}
