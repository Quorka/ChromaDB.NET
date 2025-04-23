using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ChromaDB.NET;

/// <summary>
/// A document to be stored in ChromaDB
/// </summary>
public class ChromaDocument
{
    /// <summary>
    /// Unique identifier for the document
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Vector embedding representation of the document
    /// </summary>
    public float[] Embedding { get; set; }

    /// <summary>
    /// Metadata associated with the document
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; }

    /// <summary>
    /// Document text content
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Creates a new document with the specified ID and text
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <param name="text">Document text</param>
    /// <param name="metadata">Optional metadata</param>
    /// <returns>A new ChromaDocument</returns>
    public static ChromaDocument Create(string id, string text, Dictionary<string, object> metadata = null)
    {
        return new ChromaDocument
        {
            Id = id,
            Text = text,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a new document with the specified ID and embedding
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <param name="embedding">Embedding vector</param>
    /// <param name="metadata">Optional metadata</param>
    /// <returns>A new ChromaDocument</returns>
    public static ChromaDocument CreateWithEmbedding(string id, float[] embedding, Dictionary<string, object> metadata = null)
    {
        return new ChromaDocument
        {
            Id = id,
            Embedding = embedding,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }
}
