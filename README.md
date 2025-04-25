# ChromaDB.NET

A C# client library for [ChromaDB](https://github.com/chroma-core/chroma), an AI-native vector database.

## Overview

The C# bindings provide a native interface to ChromaDB's Rust core, allowing you to use ChromaDB directly from your .NET applications without requiring an HTTP server. This wrapper is designed to be idiomatic C#, following .NET conventions and patterns.

## Features

- Direct access to ChromaDB's Rust core via P/Invoke
- Object-oriented API with proper resource management (IDisposable)
- Idiomatic C# API with fluent interfaces and LINQ-style querying
- Full CRUD operations:
  - Collection creation and management
  - Adding, updating, and upserting documents
  - Querying by vector similarity
  - Filtering by metadata with a fluent API
  - Document retrieval and deletion
- Helper classes and methods for common operations
- Proper error handling and resource cleanup

## Requirements

- .NET 8.0 or higher
- Compatible operating systems:
  - Windows x64
  - Linux x64
  - macOS x64/ARM64

## Installation

```bash
dotnet add package ChromaDB.NET
```

## Basic Usage

```csharp
using ChromaDB.NET;

// Create a client
using var client = new ChromaClient();

// Define an embedding function (replace with your actual implementation)
var embeddingFunction = new SimpleEmbeddingFunction(); // Assuming SimpleEmbeddingFunction from example

// Create or get a collection
using var collection = client.GetOrCreateCollection("documents", embeddingFunction);

// Add documents (using ChromaDocument objects as in the example)
var documentsToAdd = new List<ChromaDocument>
{
    new ChromaDocument { Id = "doc1", Text = "The quick brown fox jumps over the lazy dog", Metadata = new Dictionary<string, object> { ["source"] = "example" } },
    new ChromaDocument { Id = "doc2", Text = "Another document about something else", Metadata = new Dictionary<string, object> { ["source"] = "example" } }
};
collection.Add(documentsToAdd);

// Query the collection (using Query, not Search)
var results = collection.Query(
    queryText: "fox", 
    nResults: 5, 
    includeDocuments: true, 
    includeMetadatas: true
);

// Process results
foreach (var id in results.Ids)
{
    Console.WriteLine(id);
}
// Access other fields like results.Documents, results.Metadatas if included
```

## Idiomatic API Examples

### Filtering with the Fluent API

```csharp
// Create a filter using a dictionary (fluent API for WhereFilter not shown in example)
var filter = new Dictionary<string, object> 
{
    { "category", "article" },
    { "year", new Dictionary<string, object> { { "$gt", 2020 } } } // Example for greater than
};

// Apply the filter using Query
var results = collection.Query(
    queryText: "relevant topic", // Query requires some query text/embeddings
    whereFilter: filter,
    nResults: 10,
    includeDocuments: true
);

// Combined query and filter
var queryResults = collection.Query(
    queryText: "machine learning", 
    whereFilter: new Dictionary<string, object> { { "tags", new Dictionary<string, object> { { "$in", new[] { "AI", "ML" } } } } }, // Example for $in operator
    nResults: 10,
    includeDocuments: true
);
```

### Document Management

```csharp
// Add a document using ChromaDocument
collection.Add(new ChromaDocument { Id = "doc3", Text = "Document text", Metadata = new Dictionary<string, object> { ["key"] = "value" } });

// Get a document by ID
var docResult = collection.Get(ids: new[] { "doc3" }, includeDocuments: true, includeMetadatas: true);
if (docResult.Ids.Count > 0)
{
    Console.WriteLine($"Got doc: {docResult.Documents[0]}");
}

// Update a document (Note: Update replaces embeddings/text/metadata based on what's provided)
collection.Update(new ChromaDocument { Id = "doc3", Text = "Updated text", Metadata = new Dictionary<string, object> { ["updated"] = true } });

// Upsert a document (create if not exists, update if exists)
collection.Upsert(new ChromaDocument { Id = "doc4", Text = "New or updated document" });

// Delete documents by ID
collection.Delete(ids: new[] { "doc3", "doc4" });
```

## Using with Embedding Models

ChromaDB.NET can be used with any embedding model. Here's an example using a sentence transformer model:

```csharp
public class SentenceTransformerEmbedding : IEmbeddingFunction
{
    private readonly SentenceTransformer _model;
    
    public SentenceTransformerEmbedding(string modelName = "all-MiniLM-L6-v2")
    {
        _model = new SentenceTransformer(modelName);
    }
    
    public float[][] GenerateEmbeddings(IEnumerable<string> documents)
    {
        return _model.Encode(documents.ToArray()).ToArray();
    }
    
    public object Configuration => new { 
        name = "sentence_transformer",
        model = "all-MiniLM-L6-v2"
    };
}

// Use with ChromaDB
var embeddingFunction = new SentenceTransformerEmbedding();
var collection = client.CreateCollection("my-collection", embeddingFunction);
```

## API Reference

### ChromaClient

- `ChromaClient(string persistDirectory = null, int hnswCacheSize = 1000)` - Creates a new client
- `Collection CreateCollection(string name, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null, bool getOrCreate = false)` - Creates or gets a collection
- `Collection GetCollection(string name, IEmbeddingFunction embeddingFunction = null)` - Gets an existing collection
- `Collection GetOrCreateCollection(string name, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)` - Gets or creates a collection
- `void CreateDatabase(string name, string tenant = null)` - Creates a database
- `string GetDatabaseId(string name, string tenant = null)` - Gets a database ID (Note: Native method returns ID, C# wrapper might differ)
- `void DeleteDatabase(string name, string tenant = null)` - Deletes a database
- `ulong Heartbeat()` - Gets a timestamp from the server

### Collection

- `uint Count()` - Gets the number of documents in the collection
- `void Add(ChromaDocument document)` - Adds a single document
- `void Add(IEnumerable<ChromaDocument> documents)` - Adds multiple documents (as used in example)
- `void Update(ChromaDocument document)` - Updates a single document
- `void Update(IEnumerable<ChromaDocument> documents)` - Updates multiple documents
- `void Upsert(ChromaDocument document)` - Upserts a single document
- `void Upsert(IEnumerable<ChromaDocument> documents)` - Upserts multiple documents
- `void Delete(IEnumerable<string> ids = null, Dictionary<string, object> whereFilter = null, Dictionary<string, object> whereDocumentFilter = null)` - Deletes documents by ID or filter
- `QueryResult Get(IEnumerable<string> ids = null, Dictionary<string, object> whereFilter = null, Dictionary<string, object> whereDocumentFilter = null, uint limit = 0, uint offset = 0, bool includeEmbeddings = false, bool includeMetadatas = false, bool includeDocuments = false)` - Gets documents by ID or filter
- `QueryResult Query(IEnumerable<float[]> queryEmbeddings = null, IEnumerable<string> queryTexts = null, int nResults = 10, Dictionary<string, object> whereFilter = null, Dictionary<string, object> whereDocumentFilter = null, bool includeEmbeddings = false, bool includeMetadatas = false, bool includeDocuments = false, bool includeDistances = false)` - Queries the collection by embeddings or text

## Building from Source

### Prerequisites

- .NET 8.0 SDK or higher
- Rust toolchain (stable recommended)
- C/C++ build tools:
  - Windows: Visual Studio with C++ workload
  - Linux: GCC
  - macOS: Xcode Command Line Tools

### Build Steps

1. Clone the repository
2. Navigate to the repository root directory (where Cargo.toml is located)
3. Run the build script:
   ```bash
   # On Linux/macOS
   ./build.sh
   
   # On Windows (using PowerShell)
   .\build.ps1
   ```
   This compiles the Rust native library for your platform and copies it to the `runtimes` directory.
4. Build the .NET solution:
   ```bash
   dotnet build ChromaDB.NET.sln
   ```

## License

This project is licensed under the Apache License 2.0 - see the LICENSE file for details.