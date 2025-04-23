using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ChromaDB.NET
{
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
    }

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
    }

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

    /// <summary>
    /// Native methods for interacting with ChromaDB
    /// </summary>
    internal static class NativeMethods
    {
        private const string DllName = "chroma_csharp";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_create_client(
            [MarshalAs(UnmanagedType.I1)] bool allowReset,
            IntPtr sqliteConfigPtr,
            UIntPtr hnswCacheSize,
            [MarshalAs(UnmanagedType.LPStr)] string persistPath,
            out IntPtr clientHandle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_destroy_client(IntPtr clientHandle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_heartbeat(IntPtr clientHandle, out ulong result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_create_collection(
            IntPtr clientHandle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string configJson,
            [MarshalAs(UnmanagedType.LPStr)] string metadataJson,
            [MarshalAs(UnmanagedType.I1)] bool getOrCreate,
            [MarshalAs(UnmanagedType.LPStr)] string tenant,
            [MarshalAs(UnmanagedType.LPStr)] string database,
            out IntPtr collectionHandle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_get_collection(
            IntPtr clientHandle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string tenant,
            [MarshalAs(UnmanagedType.LPStr)] string database,
            out IntPtr collectionHandle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_destroy_collection(IntPtr collectionHandle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_add(
            IntPtr clientHandle,
            IntPtr collectionHandle,
            IntPtr ids,
            UIntPtr idsCount,
            IntPtr embeddings,
            UIntPtr embeddingDim,
            IntPtr metadatasJson,
            IntPtr documents);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_query(
            IntPtr clientHandle,
            IntPtr collectionHandle,
            IntPtr queryEmbedding,
            UIntPtr embeddingDim,
            uint nResults,
            [MarshalAs(UnmanagedType.LPStr)] string whereFilterJson,
            [MarshalAs(UnmanagedType.LPStr)] string whereDocumentFilter,
            [MarshalAs(UnmanagedType.I1)] bool includeEmbeddings,
            [MarshalAs(UnmanagedType.I1)] bool includeMetadatas,
            [MarshalAs(UnmanagedType.I1)] bool includeDocuments,
            [MarshalAs(UnmanagedType.I1)] bool includeDistances,
            out IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_free_query_result(IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_free_string(IntPtr str);
    }

    /// <summary>
    /// Exception thrown when ChromaDB operations fail
    /// </summary>
    public class ChromaException : Exception
    {
        public ChromaException(string message) : base(message) { }
    }

    /// <summary>
    /// Client for interacting with ChromaDB
    /// </summary>
    public class ChromaClient : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed = false;

        /// <summary>
        /// Creates a new ChromaDB client
        /// </summary>
        /// <param name="persistDirectory">Directory for persisting data</param>
        /// <param name="hnswCacheSize">Size of the HNSW index cache</param>
        public ChromaClient(string persistDirectory = null, int hnswCacheSize = 1000)
        {
            var result = NativeMethods.chroma_create_client(
                false, 
                IntPtr.Zero, 
                (UIntPtr)hnswCacheSize, 
                persistDirectory, 
                out _handle);
            
            if (result != 0)
                throw new ChromaException($"Failed to create ChromaDB client: {result}");
        }

        /// <summary>
        /// Creates a new collection
        /// </summary>
        /// <param name="name">Collection name</param>
        /// <param name="embeddingFunction">Function to generate embeddings</param>
        /// <param name="metadata">Collection metadata</param>
        /// <returns>A Collection object</returns>
        public Collection CreateCollection(string name, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)
        {
            string configJson = null;
            if (embeddingFunction != null)
            {
                var config = new Dictionary<string, object>
                {
                    ["embedding_function"] = embeddingFunction.Configuration
                };
                configJson = JsonSerializer.Serialize(config);
            }
            
            string metadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null;
            
            var result = NativeMethods.chroma_create_collection(
                _handle, 
                name, 
                configJson, 
                metadataJson, 
                false, 
                null, 
                null, 
                out var collectionHandle);
                
            if (result != 0)
                throw new ChromaException($"Failed to create collection: {result}");
                
            return new Collection(this, collectionHandle, embeddingFunction);
        }

        /// <summary>
        /// Gets an existing collection
        /// </summary>
        /// <param name="name">Collection name</param>
        /// <param name="embeddingFunction">Function to generate embeddings</param>
        /// <returns>A Collection object</returns>
        public Collection GetCollection(string name, IEmbeddingFunction embeddingFunction = null)
        {
            var result = NativeMethods.chroma_get_collection(
                _handle, 
                name, 
                null, 
                null, 
                out var collectionHandle);
                
            if (result != 0)
                throw new ChromaException($"Failed to get collection: {result}");
                
            return new Collection(this, collectionHandle, embeddingFunction);
        }

        /// <summary>
        /// Gets a heartbeat from the server
        /// </summary>
        /// <returns>Current timestamp</returns>
        public ulong Heartbeat()
        {
            var result = NativeMethods.chroma_heartbeat(_handle, out var timestamp);
            
            if (result != 0)
                throw new ChromaException($"Failed to get heartbeat: {result}");
                
            return timestamp;
        }

        /// <summary>
        /// Disposes the client
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the client
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    NativeMethods.chroma_destroy_client(_handle);
                    _handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~ChromaClient()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the native handle
        /// </summary>
        internal IntPtr Handle => _handle;
    }

    /// <summary>
    /// A collection in ChromaDB
    /// </summary>
    public class Collection : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed = false;
        private readonly ChromaClient _client;
        private readonly IEmbeddingFunction _embeddingFunction;

        internal Collection(ChromaClient client, IntPtr handle, IEmbeddingFunction embeddingFunction)
        {
            _client = client;
            _handle = handle;
            _embeddingFunction = embeddingFunction;
        }

        /// <summary>
        /// Adds documents to the collection
        /// </summary>
        /// <param name="documents">Documents to add</param>
        public void Add(IEnumerable<ChromaDocument> documents)
        {
            var docs = documents.ToList();
            if (docs.Count == 0)
                return;
                
            // Generate embeddings if needed
            if (_embeddingFunction != null)
            {
                var textsToEmbed = docs.Where(d => d.Embedding == null && !string.IsNullOrEmpty(d.Text))
                    .Select(d => d.Text)
                    .ToList();
                    
                if (textsToEmbed.Count > 0)
                {
                    var embeddings = _embeddingFunction.GenerateEmbeddings(textsToEmbed);
                    int index = 0;
                    
                    foreach (var doc in docs.Where(d => d.Embedding == null && !string.IsNullOrEmpty(d.Text)))
                    {
                        doc.Embedding = embeddings[index++];
                    }
                }
            }
            
            // Marshal IDs
            var ids = docs.Select(d => d.Id).ToArray();
            var idsPtr = MarshalStringArray(ids);
            
            // Marshal embeddings
            var embeddingDim = docs[0].Embedding.Length;
            var embeddingsPtr = MarshalEmbeddings(docs.Select(d => d.Embedding).ToArray());
            
            // Marshal metadata
            var metadataJsons = docs.Select(d => d.Metadata != null 
                ? JsonSerializer.Serialize(d.Metadata) 
                : null).ToArray();
            var metadataPtr = MarshalStringArray(metadataJsons);
            
            // Marshal documents
            var texts = docs.Select(d => d.Text).ToArray();
            var textsPtr = MarshalStringArray(texts);
            
            try
            {
                var result = NativeMethods.chroma_add(
                    _client.Handle,
                    _handle,
                    idsPtr,
                    (UIntPtr)ids.Length,
                    embeddingsPtr,
                    (UIntPtr)embeddingDim,
                    metadataPtr,
                    textsPtr);
                    
                if (result != 0)
                    throw new ChromaException($"Failed to add documents: {result}");
            }
            finally
            {
                FreeStringArray(idsPtr, ids.Length);
                FreeStringArray(metadataPtr, metadataJsons.Length);
                FreeStringArray(textsPtr, texts.Length);
                FreeEmbeddings(embeddingsPtr, docs.Count);
            }
        }

        /// <summary>
        /// Queries the collection for similar documents
        /// </summary>
        /// <param name="queryEmbedding">Query embedding vector</param>
        /// <param name="nResults">Number of results to return</param>
        /// <param name="whereFilter">Metadata filter</param>
        /// <param name="whereDocument">Document content filter</param>
        /// <param name="includeMetadatas">Include metadata in results</param>
        /// <param name="includeDocuments">Include document text in results</param>
        /// <param name="includeDistances">Include distance scores in results</param>
        /// <returns>Query results</returns>
        public QueryResult Query(
            float[] queryEmbedding,
            int nResults = 10,
            Dictionary<string, object> whereFilter = null,
            string whereDocument = null,
            bool includeMetadatas = true,
            bool includeDocuments = true,
            bool includeDistances = true)
        {
            var whereFilterJson = whereFilter != null ? JsonSerializer.Serialize(whereFilter) : null;
            
            var embeddingPtr = Marshal.AllocHGlobal(queryEmbedding.Length * sizeof(float));
            Marshal.Copy(queryEmbedding, 0, embeddingPtr, queryEmbedding.Length);
            
            try
            {
                var result = NativeMethods.chroma_query(
                    _client.Handle,
                    _handle,
                    embeddingPtr,
                    (UIntPtr)queryEmbedding.Length,
                    (uint)nResults,
                    whereFilterJson,
                    whereDocument,
                    false, // Don't include embeddings in response for simplicity
                    includeMetadatas,
                    includeDocuments,
                    includeDistances,
                    out var queryResultPtr);
                    
                if (result != 0)
                    throw new ChromaException($"Failed to query: {result}");
                    
                try
                {
                    return MarshalQueryResult(queryResultPtr);
                }
                finally
                {
                    NativeMethods.chroma_free_query_result(queryResultPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(embeddingPtr);
            }
        }

        /// <summary>
        /// Queries the collection using text
        /// </summary>
        /// <param name="queryText">Text to search for</param>
        /// <param name="nResults">Number of results to return</param>
        /// <param name="whereFilter">Metadata filter</param>
        /// <param name="whereDocument">Document content filter</param>
        /// <param name="includeMetadatas">Include metadata in results</param>
        /// <param name="includeDocuments">Include document text in results</param>
        /// <param name="includeDistances">Include distance scores in results</param>
        /// <returns>Query results</returns>
        public QueryResult Query(
            string queryText,
            int nResults = 10,
            Dictionary<string, object> whereFilter = null,
            string whereDocument = null,
            bool includeMetadatas = true,
            bool includeDocuments = true,
            bool includeDistances = true)
        {
            if (_embeddingFunction == null)
                throw new ChromaException("Cannot query by text without an embedding function");
                
            var queryEmbedding = _embeddingFunction.GenerateEmbeddings(new[] { queryText })[0];
            
            return Query(
                queryEmbedding,
                nResults,
                whereFilter,
                whereDocument,
                includeMetadatas,
                includeDocuments,
                includeDistances);
        }

        /// <summary>
        /// Disposes the collection
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the collection
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    NativeMethods.chroma_destroy_collection(_handle);
                    _handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~Collection()
        {
            Dispose(false);
        }

        #region Marshaling Helpers

        private static IntPtr MarshalStringArray(string[] strings)
        {
            if (strings == null || strings.Length == 0)
                return IntPtr.Zero;

            var ptrs = new IntPtr[strings.Length];
            for (int i = 0; i < strings.Length; i++)
            {
                ptrs[i] = strings[i] != null 
                    ? Marshal.StringToHGlobalAnsi(strings[i]) 
                    : IntPtr.Zero;
            }

            var arrayPtr = Marshal.AllocHGlobal(IntPtr.Size * strings.Length);
            Marshal.Copy(ptrs, 0, arrayPtr, strings.Length);
            
            return arrayPtr;
        }

        private static void FreeStringArray(IntPtr arrayPtr, int length)
        {
            if (arrayPtr == IntPtr.Zero)
                return;

            var ptrs = new IntPtr[length];
            Marshal.Copy(arrayPtr, ptrs, 0, length);
            
            for (int i = 0; i < length; i++)
            {
                if (ptrs[i] != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptrs[i]);
            }
            
            Marshal.FreeHGlobal(arrayPtr);
        }

        private static IntPtr MarshalEmbeddings(float[][] embeddings)
        {
            if (embeddings == null || embeddings.Length == 0)
                return IntPtr.Zero;

            var ptrs = new IntPtr[embeddings.Length];
            for (int i = 0; i < embeddings.Length; i++)
            {
                var embedding = embeddings[i];
                if (embedding != null)
                {
                    ptrs[i] = Marshal.AllocHGlobal(embedding.Length * sizeof(float));
                    Marshal.Copy(embedding, 0, ptrs[i], embedding.Length);
                }
                else
                {
                    ptrs[i] = IntPtr.Zero;
                }
            }

            var arrayPtr = Marshal.AllocHGlobal(IntPtr.Size * embeddings.Length);
            Marshal.Copy(ptrs, 0, arrayPtr, embeddings.Length);
            
            return arrayPtr;
        }

        private static void FreeEmbeddings(IntPtr arrayPtr, int length)
        {
            if (arrayPtr == IntPtr.Zero)
                return;

            var ptrs = new IntPtr[length];
            Marshal.Copy(arrayPtr, ptrs, 0, length);
            
            for (int i = 0; i < length; i++)
            {
                if (ptrs[i] != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptrs[i]);
            }
            
            Marshal.FreeHGlobal(arrayPtr);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ChromaQueryResultNative
        {
            public IntPtr Ids;
            public UIntPtr IdsCount;
            public IntPtr Distances;
            public UIntPtr DistancesCount;
            public IntPtr MetadataJson;
            public UIntPtr MetadataCount;
            public IntPtr Documents;
            public UIntPtr DocumentsCount;
        }

        private static QueryResult MarshalQueryResult(IntPtr resultPtr)
        {
            if (resultPtr == IntPtr.Zero)
                return new QueryResult();

            var nativeResult = Marshal.PtrToStructure<ChromaQueryResultNative>(resultPtr);
            var result = new QueryResult();

            // Marshal IDs
            if (nativeResult.Ids != IntPtr.Zero && nativeResult.IdsCount.ToUInt64() > 0)
            {
                var count = (int)nativeResult.IdsCount.ToUInt64();
                var idsPtrs = new IntPtr[count];
                Marshal.Copy(nativeResult.Ids, idsPtrs, 0, count);
                
                for (int i = 0; i < count; i++)
                {
                    if (idsPtrs[i] != IntPtr.Zero)
                    {
                        var id = Marshal.PtrToStringAnsi(idsPtrs[i]);
                        result.Ids.Add(id);
                    }
                    else
                    {
                        result.Ids.Add(null);
                    }
                }
            }

            // Marshal distances
            if (nativeResult.Distances != IntPtr.Zero && nativeResult.DistancesCount.ToUInt64() > 0)
            {
                var count = (int)nativeResult.DistancesCount.ToUInt64();
                var distances = new float[count];
                Marshal.Copy(nativeResult.Distances, distances, 0, count);
                result.Distances.AddRange(distances);
            }

            // Marshal metadata
            if (nativeResult.MetadataJson != IntPtr.Zero && nativeResult.MetadataCount.ToUInt64() > 0)
            {
                var count = (int)nativeResult.MetadataCount.ToUInt64();
                var metadataPtrs = new IntPtr[count];
                Marshal.Copy(nativeResult.MetadataJson, metadataPtrs, 0, count);
                
                for (int i = 0; i < count; i++)
                {
                    if (metadataPtrs[i] != IntPtr.Zero)
                    {
                        var json = Marshal.PtrToStringAnsi(metadataPtrs[i]);
                        if (!string.IsNullOrEmpty(json))
                        {
                            try
                            {
                                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                                result.Metadatas.Add(metadata);
                            }
                            catch
                            {
                                result.Metadatas.Add(new Dictionary<string, object>());
                            }
                        }
                        else
                        {
                            result.Metadatas.Add(new Dictionary<string, object>());
                        }
                    }
                    else
                    {
                        result.Metadatas.Add(new Dictionary<string, object>());
                    }
                }
            }

            // Marshal documents
            if (nativeResult.Documents != IntPtr.Zero && nativeResult.DocumentsCount.ToUInt64() > 0)
            {
                var count = (int)nativeResult.DocumentsCount.ToUInt64();
                var documentPtrs = new IntPtr[count];
                Marshal.Copy(nativeResult.Documents, documentPtrs, 0, count);
                
                for (int i = 0; i < count; i++)
                {
                    if (documentPtrs[i] != IntPtr.Zero)
                    {
                        var document = Marshal.PtrToStringAnsi(documentPtrs[i]);
                        result.Documents.Add(document);
                    }
                    else
                    {
                        result.Documents.Add(null);
                    }
                }
            }

            return result;
        }

        #endregion
    }
}