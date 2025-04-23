using ChromaDB.NET;
using System.Text.Json;

namespace ChromaDB.Example
{
    // Simple local embedding function for demonstration purposes
    public class SimpleEmbeddingFunction : IEmbeddingFunction
    {
        private readonly int _dimensions;
        private readonly Random _random;

        public SimpleEmbeddingFunction(int dimensions = 1536)
        {
            _dimensions = dimensions;
            _random = new Random(42); // Fixed seed for reproducibility
        }

        public float[][] GenerateEmbeddings(IEnumerable<string> documents)
        {
            // This is a very simple embedding function that just creates random vectors
            // In a real application, you would use a proper embedding model
            return documents.Select(_ => Enumerable.Range(0, _dimensions)
                .Select(__ => (float)_random.NextDouble())
                .ToArray())
                .ToArray();
        }

        public object Configuration => new { model_name = "simple_random_embeddings" };
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ChromaDB.NET Basic Example");
            Console.WriteLine("=========================");

            try
            {
                // Define a temporary directory for ChromaDB data
                string dataDir = Path.Combine(Path.GetTempPath(), "chromadb-dotnet-example");
                Directory.CreateDirectory(dataDir);
                Console.WriteLine($"Using data directory: {dataDir}");

                // Create a simple embedding function
                var embeddingFunction = new SimpleEmbeddingFunction(dimensions: 3);

                // Create a ChromaDB client
                using var client = new ChromaClient(persistDirectory: dataDir, hnswCacheSize: 100);

                // Create a collection
                var collection = client.CreateCollection(
                    name: "example-collection",
                    embeddingFunction: embeddingFunction,
                    metadata: new Dictionary<string, object> { { "description", "Example collection" } }
                );

                Console.WriteLine("Collection created successfully");

                // Create some sample documents
                var documents = new List<ChromaDocument>
                {
                    new ChromaDocument
                    {
                        Id = "doc1",
                        Text = "The quick brown fox jumps over the lazy dog",
                        Metadata = new Dictionary<string, object> { { "source", "example" }, { "category", "animals" } }
                    },
                    new ChromaDocument
                    {
                        Id = "doc2",
                        Text = "A rainbow appears after the rain",
                        Metadata = new Dictionary<string, object> { { "source", "example" }, { "category", "weather" } }
                    },
                    new ChromaDocument
                    {
                        Id = "doc3",
                        Text = "The dog barked at the mailman",
                        Metadata = new Dictionary<string, object> { { "source", "example" }, { "category", "animals" } }
                    }
                };

                // Add documents to the collection
                collection.Add(documents);
                Console.WriteLine("Added documents to collection");

                // Query the collection
                Console.WriteLine("\nQuerying for documents about dogs:");
                var results = collection.Query(
                    queryText: "dog",
                    nResults: 2,
                    includeMetadatas: true,
                    includeDocuments: true,
                    includeDistances: true
                );

                // Display results
                for (int i = 0; i < results.Ids.Count; i++)
                {
                    Console.WriteLine($"Document ID: {results.Ids[i]}");
                    Console.WriteLine($"Distance: {results.Distances[i]}");
                    Console.WriteLine($"Text: {results.Documents[i]}");
                    Console.WriteLine($"Metadata: {JsonSerializer.Serialize(results.Metadatas[i])}");
                    Console.WriteLine();
                }

                // Query with metadata filter
                Console.WriteLine("\nQuerying for documents in the 'animals' category:");
                results = collection.Query(
                    queryText: "animal",
                    nResults: 10,
                    whereFilter: new Dictionary<string, object> { { "category", "animals" } },
                    includeMetadatas: true,
                    includeDocuments: true
                );

                // Display results
                for (int i = 0; i < results.Ids.Count; i++)
                {
                    Console.WriteLine($"Document ID: {results.Ids[i]}");
                    Console.WriteLine($"Text: {results.Documents[i]}");
                    Console.WriteLine($"Metadata: {JsonSerializer.Serialize(results.Metadatas[i])}");
                    Console.WriteLine();
                }

                Console.WriteLine("Example completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}