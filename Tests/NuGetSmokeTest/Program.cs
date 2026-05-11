using ChromaDB.NET;

var testDir = Path.Combine(Path.GetTempPath(), "chromadb-nuget-smoke", Guid.NewGuid().ToString());
Directory.CreateDirectory(testDir);

try
{
    Console.WriteLine("Creating ChromaClient...");
    using var client = new ChromaClient(persistDirectory: testDir);

    Console.WriteLine("Heartbeat...");
    var heartbeat = client.Heartbeat();
    if (heartbeat == 0)
        Fail("Heartbeat returned 0");

    Console.WriteLine("Creating collection...");
    var embedder = new SimpleEmbedder();
    using var collection = client.CreateCollection("smoke-test", embedder);

    Console.WriteLine("Adding documents...");
    collection.Add("doc1", "The quick brown fox jumps over the lazy dog",
        new Dictionary<string, object> { ["source"] = "smoke-test" });
    collection.Add("doc2", "ChromaDB is a vector database for AI applications",
        new Dictionary<string, object> { ["source"] = "smoke-test" });

    Console.WriteLine("Verifying count...");
    var count = collection.Count();
    if (count != 2)
        Fail($"Expected count 2, got {count}");

    Console.WriteLine("Querying...");
    var results = collection.Query(queryText: "database", nResults: 2, includeDocuments: true);
    if (results.Ids.Count != 2)
        Fail($"Expected 2 results, got {results.Ids.Count}");

    Console.WriteLine("Getting by ID...");
    var doc = collection.GetById("doc1");
    if (doc == null || doc.Text != "The quick brown fox jumps over the lazy dog")
        Fail("GetById returned unexpected result");

    Console.WriteLine("Deleting...");
    collection.Delete("doc1");
    if (collection.Count() != 1)
        Fail("Delete did not reduce count");

    Console.WriteLine("NuGet smoke test passed.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"SMOKE TEST FAILED: {ex}");
    return 1;
}
finally
{
    try { Directory.Delete(testDir, true); } catch { }
}

static void Fail(string message)
{
    throw new Exception(message);
}

class SimpleEmbedder : IEmbeddingFunction
{
    private readonly Random _rng = new(42);
    public object Configuration => new { model_name = "smoke_test" };

    public float[][] GenerateEmbeddings(IEnumerable<string> documents)
    {
        return documents.Select(_ =>
        {
            var v = new float[4];
            for (int i = 0; i < v.Length; i++)
                v[i] = (float)_rng.NextDouble();
            return v;
        }).ToArray();
    }
}
