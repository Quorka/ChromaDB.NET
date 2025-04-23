# ChromaDB.NET

A C# client library for [ChromaDB](https://github.com/chroma-core/chroma), an AI-native vector database.

## Installation

```bash
dotnet add package ChromaDB.NET
```

## Features

- Native bindings to the Rust core of ChromaDB
- High-performance vector search operations
- Easy-to-use C# API with idiomatic design
- Cross-platform support (Windows, Linux, macOS)
- Async API support
- Compatible with .NET 6.0 and higher

## Basic Usage

```csharp
using ChromaDB.NET;

// Create a ChromaDB client
using var client = new ChromaClient(persistDirectory: "/path/to/data");

// Create a collection
var collection = client.CreateCollection("my-collection");

// Add documents
collection.Add(new List<ChromaDocument>
{
    new ChromaDocument
    {
        Id = "doc1",
        Text = "This is a sample document",
        Embedding = new float[] { 0.1f, 0.2f, 0.3f },
        Metadata = new Dictionary<string, object> { { "source", "example" } }
    },
    new ChromaDocument
    {
        Id = "doc2",
        Text = "Another example document",
        Embedding = new float[] { 0.2f, 0.3f, 0.4f },
        Metadata = new Dictionary<string, object> { { "source", "example" } }
    }
});

// Query for similar documents
var results = collection.Query(
    queryText: "sample document",
    nResults: 2,
    whereFilter: new Dictionary<string, object> { { "source", "example" } }
);

// Process results
foreach (var id in results.Ids)
{
    Console.WriteLine($"Found document: {id}");
}
```

## Using with Embedding Models

ChromaDB.NET can be used with any embedding model. Here's an example using an OpenAI embedding function:

```csharp
public class OpenAIEmbeddingFunction : IEmbeddingFunction
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    
    public OpenAIEmbeddingFunction(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }
    
    public float[][] GenerateEmbeddings(IEnumerable<string> documents)
    {
        // Implementation to call OpenAI's embedding API
        // ...
    }
    
    public object Configuration => new { api_key = _apiKey };
}

// Use with ChromaDB
var embeddingFunction = new OpenAIEmbeddingFunction(apiKey: "your-openai-key");
var collection = client.CreateCollection("my-collection", embeddingFunction);
```

## Building from Source

To build the library from source:

1. Clone the repository
2. Install Rust and .NET SDK
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