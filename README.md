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

- .NET 6.0 or higher
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

// Create an embedding function
var embeddingFunction = new MyEmbeddingFunction();

// Create or get a collection
using var collection = client.GetOrCreateCollection("documents", embeddingFunction);

// Add documents
collection.Add("doc1", "The quick brown fox jumps over the lazy dog",
    new Dictionary<string, object> { ["source"] = "example" });

// Query the collection
var results = collection.Search("fox", limit: 5);

// Process results
foreach (var id in results.Ids)
{
    Console.WriteLine(id);
}
```

## Idiomatic API Examples

### Filtering with the Fluent API

```csharp
// Create a filter with the fluent API
var filter = new WhereFilter()
    .Equals("category", "article")
    .GreaterThan("year", 2020);

// Apply the filter
var results = collection.Where(filter);

// Combined search and filter
var searchResults = collection.Search(
    text: "machine learning", 
    filter: new WhereFilter().In("tags", new[] { "AI", "ML" })
);
```

### Document Management

```csharp
// Add a document
collection.Add("doc1", "Document text", new Dictionary<string, object> { ["key"] = "value" });

// Get a document by ID
var doc = collection.GetById("doc1");

// Update a document
collection.Update("doc1", "Updated text", new Dictionary<string, object> { ["updated"] = true });

// Upsert a document (create if not exists, update if exists)
collection.Upsert("doc2", "New or updated document");

// Delete a document
collection.Delete("doc1");
```

### Converting Results

```csharp
// Get results as ChromaDocument objects
var documents = results.ToDocuments();

// Get first result or null
var firstDoc = results.FirstOrDefault();
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
- `Collection CreateCollection(string name, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)` - Creates a new collection
- `Collection GetCollection(string name, IEmbeddingFunction embeddingFunction = null)` - Gets an existing collection
- `Collection GetOrCreateCollection(string name, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)` - Gets or creates a collection
- `void CreateDatabase(string name, string tenant = null)` - Creates a database
- `string GetDatabaseId(string name, string tenant = null)` - Gets a database ID
- `void DeleteDatabase(string name, string tenant = null)` - Deletes a database
- `ulong Heartbeat()` - Gets a timestamp from the server

### Collection

- `uint Count()` - Gets the number of documents in the collection
- `void Add(ChromaDocument document)` - Adds a document to the collection
- `void Add(string id, string text, Dictionary<string, object> metadata = null)` - Adds a document with the given ID and text
- `void Update(ChromaDocument document)` - Updates a document
- `void Update(string id, string text, Dictionary<string, object> metadata = null)` - Updates a document
- `void Upsert(ChromaDocument document)` - Upserts a document
- `void Upsert(string id, string text, Dictionary<string, object> metadata = null)` - Upserts a document
- `void Delete(string id)` - Deletes a document by ID
- `ChromaDocument GetById(string id, bool includeEmbedding = false)` - Gets a document by ID
- `QueryResult Where(WhereFilter filter, uint limit = 0, uint offset = 0, bool includeEmbeddings = false)` - Gets documents matching the filter
- `QueryResult Where(string field, object value, uint limit = 0, uint offset = 0, bool includeEmbeddings = false)` - Gets documents where field equals value
- `QueryResult Search(string text, int limit = 10, WhereFilter filter = null)` - Searches for similar documents
- `QueryResult Search(string text, string field, object value, int limit = 10)` - Searches with a field filter

## Building from Source

### Prerequisites

- .NET 6.0 SDK or higher
- Rust toolchain (1.67+)
- C/C++ build tools:
  - Windows: Visual Studio with C++ workload
  - Linux: GCC
  - macOS: Xcode Command Line Tools

### Build Steps

1. Clone the repository
2. Navigate to the `rust/csharp_bindings` directory
3. Run the build script:
   ```bash
   # On Linux/macOS
   ./build.sh
   
   # On Windows
   ./build.ps1
   ```
4. Build the .NET project:
   ```bash
   dotnet build
   ```

## License

This project is licensed under the Apache License 2.0 - see the LICENSE file for details.