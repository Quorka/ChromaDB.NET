using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ChromaDB.NET;

/// <summary>
/// Result of a query operation
/// </summary>
public class QueryResult
{
    /// <summary>
    /// Document IDs
    /// </summary>
    public List<string> Ids { get; set; } = new List<string>();

    /// <summary>
    /// Distance scores (lower is more similar)
    /// </summary>
    public List<float> Distances { get; set; } = new List<float>();

    /// <summary>
    /// Document metadata
    /// </summary>
    public List<Dictionary<string, object>> Metadatas { get; set; } = new List<Dictionary<string, object>>();

    /// <summary>
    /// Document contents
    /// </summary>
    public List<string> Documents { get; set; } = new List<string>();

    /// <summary>
    /// Gets the number of results
    /// </summary>
    public int Count => Ids.Count;

    /// <summary>
    /// Gets the documents as a list of ChromaDocument objects
    /// </summary>
    /// <returns>List of ChromaDocument objects</returns>
    public List<ChromaDocument> ToDocuments()
    {
        var results = new List<ChromaDocument>();

        for (int i = 0; i < Ids.Count; i++)
        {
            var doc = new ChromaDocument
            {
                Id = Ids[i],
                Text = i < Documents.Count ? Documents[i] : null,
                Metadata = i < Metadatas.Count ? Metadatas[i] : null
            };

            results.Add(doc);
        }

        return results;
    }

    /// <summary>
    /// Gets the first document from the results
    /// </summary>
    /// <returns>First document or null if no results</returns>
    public ChromaDocument FirstOrDefault()
    {
        if (Ids.Count == 0)
            return null;

        return new ChromaDocument
        {
            Id = Ids[0],
            Text = Documents.Count > 0 ? Documents[0] : null,
            Metadata = Metadatas.Count > 0 ? Metadatas[0] : null
        };
    }
}

