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

    /// <summary>
    /// A builder class for filters in ChromaDB
    /// </summary>
    public class WhereFilter
    {
        private readonly Dictionary<string, object> _filter = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new filter
        /// </summary>
        public WhereFilter() { }

        /// <summary>
        /// Adds an equals condition to the filter
        /// </summary>
        /// <param name="field">Field name</param>
        /// <param name="value">Value to match</param>
        /// <returns>This filter instance for chaining</returns>
        public WhereFilter Equals(string field, object value)
        {
            _filter[field] = value;
            return this;
        }

        /// <summary>
        /// Adds an $in condition to the filter
        /// </summary>
        /// <param name="field">Field name</param>
        /// <param name="values">Values to match</param>
        /// <returns>This filter instance for chaining</returns>
        public WhereFilter In(string field, IEnumerable<object> values)
        {
            _filter[field] = new Dictionary<string, object>
            {
                ["$in"] = values.ToList()
            };
            return this;
        }

        /// <summary>
        /// Adds a $nin (not in) condition to the filter
        /// </summary>
        /// <param name="field">Field name</param>
        /// <param name="values">Values to exclude</param>
        /// <returns>This filter instance for chaining</returns>
        public WhereFilter NotIn(string field, IEnumerable<object> values)
        {
            _filter[field] = new Dictionary<string, object>
            {
                ["$nin"] = values.ToList()
            };
            return this;
        }

        /// <summary>
        /// Adds a $gt (greater than) condition to the filter
        /// </summary>
        /// <param name="field">Field name</param>
        /// <param name="value">Value to compare against</param>
        /// <returns>This filter instance for chaining</returns>
        public WhereFilter GreaterThan(string field, object value)
        {
            _filter[field] = new Dictionary<string, object>
            {
                ["$gt"] = value
            };
            return this;
        }

        /// <summary>
        /// Adds a $gte (greater than or equal) condition to the filter
        /// </summary>
        /// <param name="field">Field name</param>
        /// <param name="value">Value to compare against</param>
        /// <returns>This filter instance for chaining</returns>
        public WhereFilter GreaterThanOrEqual(string field, object value)
        {
            _filter[field] = new Dictionary<string, object>
            {
                ["$gte"] = value
            };
            return this;
        }

        /// <summary>
        /// Adds a $lt (less than) condition to the filter
        /// </summary>
        /// <param name="field">Field name</param>
        /// <param name="value">Value to compare against</param>
        /// <returns>This filter instance for chaining</returns>
        public WhereFilter LessThan(string field, object value)
        {
            _filter[field] = new Dictionary<string, object>
            {
                ["$lt"] = value
            };
            return this;
        }

        /// <summary>
        /// Adds a $lte (less than or equal) condition to the filter
        /// </summary>
        /// <param name="field">Field name</param>
        /// <param name="value">Value to compare against</param>
        /// <returns>This filter instance for chaining</returns>
        public WhereFilter LessThanOrEqual(string field, object value)
        {
            _filter[field] = new Dictionary<string, object>
            {
                ["$lte"] = value
            };
            return this;
        }

        /// <summary>
        /// Converts this filter to a dictionary
        /// </summary>
        public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>(_filter);

        /// <summary>
        /// Implicitly converts a WhereFilter to a Dictionary
        /// </summary>
        public static implicit operator Dictionary<string, object>(WhereFilter filter) => filter.ToDictionary();
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
        public static extern int chroma_free_error(IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_create_client(
            [MarshalAs(UnmanagedType.I1)] bool allowReset,
            IntPtr sqliteConfigPtr,
            UIntPtr hnswCacheSize,
            [MarshalAs(UnmanagedType.LPStr)] string persistPath,
            out IntPtr clientHandle,
            out IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_destroy_client(IntPtr clientHandle, out IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_heartbeat(IntPtr clientHandle, out ulong result, out IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_create_collection(
            IntPtr clientHandle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string configJson,
            [MarshalAs(UnmanagedType.LPStr)] string metadataJson,
            [MarshalAs(UnmanagedType.I1)] bool getOrCreate,
            [MarshalAs(UnmanagedType.LPStr)] string tenant,
            [MarshalAs(UnmanagedType.LPStr)] string database,
            out IntPtr collectionHandle,
            out IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_get_collection(
            IntPtr clientHandle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string tenant,
            [MarshalAs(UnmanagedType.LPStr)] string database,
            out IntPtr collectionHandle,
            out IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_destroy_collection(IntPtr collectionHandle, out IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_add(
            IntPtr clientHandle,
            IntPtr collectionHandle,
            IntPtr ids,
            UIntPtr idsCount,
            IntPtr embeddings,
            UIntPtr embeddingDim,
            IntPtr metadatasJson,
            IntPtr documents,
            out IntPtr error);

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

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_count(
            IntPtr clientHandle,
            IntPtr collectionHandle,
            out uint result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_update(
            IntPtr clientHandle,
            IntPtr collectionHandle,
            IntPtr ids,
            UIntPtr idsCount,
            IntPtr embeddings,
            UIntPtr embeddingDim,
            IntPtr metadatasJson,
            IntPtr documents);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_upsert(
            IntPtr clientHandle,
            IntPtr collectionHandle,
            IntPtr ids,
            UIntPtr idsCount,
            IntPtr embeddings,
            UIntPtr embeddingDim,
            IntPtr metadatasJson,
            IntPtr documents);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_delete(
            IntPtr clientHandle,
            IntPtr collectionHandle,
            IntPtr ids,
            UIntPtr idsCount,
            [MarshalAs(UnmanagedType.LPStr)] string whereFilterJson,
            [MarshalAs(UnmanagedType.LPStr)] string whereDocumentFilter);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_get(
            IntPtr clientHandle,
            IntPtr collectionHandle,
            IntPtr ids,
            UIntPtr idsCount,
            [MarshalAs(UnmanagedType.LPStr)] string whereFilterJson,
            [MarshalAs(UnmanagedType.LPStr)] string whereDocumentFilter,
            uint limit,
            uint offset,
            [MarshalAs(UnmanagedType.I1)] bool includeEmbeddings,
            [MarshalAs(UnmanagedType.I1)] bool includeMetadatas,
            [MarshalAs(UnmanagedType.I1)] bool includeDocuments,
            out IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_create_database(
            IntPtr clientHandle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string tenant,
            out IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_get_database(
            IntPtr clientHandle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string tenant,
            out IntPtr idResult,
            out IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int chroma_delete_database(
            IntPtr clientHandle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string tenant,
            out IntPtr error);
    }

    /// <summary>
    /// Error codes for ChromaDB operations
    /// </summary>
    public enum ChromaErrorCode
    {
        /// <summary>Operation completed successfully</summary>
        Success = 0,

        /// <summary>Invalid argument provided</summary>
        InvalidArgument = 1,

        /// <summary>Internal error occurred</summary>
        InternalError = 2,

        /// <summary>Memory allocation error</summary>
        MemoryError = 3,

        /// <summary>Resource not found</summary>
        NotFound = 4,

        /// <summary>Validation error</summary>
        ValidationError = 5,

        /// <summary>Invalid UUID</summary>
        InvalidUuid = 6,

        /// <summary>Operation not implemented</summary>
        NotImplemented = 7
    }

    /// <summary>
    /// Native representation of a ChromaDB error
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ChromaErrorNative
    {
        public ChromaErrorCode Code;
        public IntPtr Message;
        public IntPtr Source;
        public IntPtr Details;
    }

    /// <summary>
    /// Detailed error information for ChromaDB operations
    /// </summary>
    public class ChromaErrorInfo
    {
        /// <summary>Error code</summary>
        public ChromaErrorCode Code { get; }

        /// <summary>Error message</summary>
        public string Message { get; }

        /// <summary>Source of the error (function name)</summary>
        public string Source { get; }

        /// <summary>Additional error details</summary>
        public string Details { get; }

        internal ChromaErrorInfo(ChromaErrorCode code, string message, string source, string details)
        {
            Code = code;
            Message = message ?? string.Empty;
            Source = source ?? string.Empty;
            Details = details ?? string.Empty;
        }

        /// <summary>
        /// Returns a string representation of the error
        /// </summary>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.Append($"[{Code}] {Message}");

            if (!string.IsNullOrEmpty(Source))
                builder.Append($" (in {Source})");

            if (!string.IsNullOrEmpty(Details))
                builder.Append($"\nDetails: {Details}");

            return builder.ToString();
        }
    }

    /// <summary>
    /// Exception thrown when ChromaDB operations fail
    /// </summary>
    public class ChromaException : Exception
    {
        /// <summary>
        /// Detailed error information
        /// </summary>
        public ChromaErrorInfo ErrorInfo { get; }

        /// <summary>
        /// Creates a new ChromaException with the specified message
        /// </summary>
        /// <param name="message">Error message</param>
        public ChromaException(string message) : base(message)
        {
            ErrorInfo = new ChromaErrorInfo(ChromaErrorCode.InternalError, message, null, null);
        }

        /// <summary>
        /// Creates a new ChromaException with the specified error information
        /// </summary>
        /// <param name="errorInfo">Detailed error information</param>
        public ChromaException(ChromaErrorInfo errorInfo)
            : base(errorInfo.ToString())
        {
            ErrorInfo = errorInfo;
        }

        /// <summary>
        /// Creates a new ChromaException with the specified error code and message
        /// </summary>
        /// <param name="code">Error code</param>
        /// <param name="message">Error message</param>
        public ChromaException(ChromaErrorCode code, string message)
            : base(message)
        {
            ErrorInfo = new ChromaErrorInfo(code, message, null, null);
        }
    }

    /// <summary>
    /// Client for interacting with ChromaDB
    /// </summary>
    public class ChromaClient : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed = false;

        /// <summary>
        /// Helper method to marshal a native ChromaError to a managed ChromaErrorInfo
        /// </summary>
        internal static ChromaErrorInfo MarshalError(IntPtr errorPtr)
        {
            if (errorPtr == IntPtr.Zero)
                return new ChromaErrorInfo(ChromaErrorCode.InternalError, "Unknown error", null, null);

            var nativeError = Marshal.PtrToStructure<ChromaErrorNative>(errorPtr);

            string message = null;
            if (nativeError.Message != IntPtr.Zero)
                message = Marshal.PtrToStringAnsi(nativeError.Message);

            string source = null;
            if (nativeError.Source != IntPtr.Zero)
                source = Marshal.PtrToStringAnsi(nativeError.Source);

            string details = null;
            if (nativeError.Details != IntPtr.Zero)
                details = Marshal.PtrToStringAnsi(nativeError.Details);

            return new ChromaErrorInfo(nativeError.Code, message, source, details);
        }

        /// <summary>
        /// Helper method to check for errors and throw exceptions if needed
        /// </summary>
        internal static void CheckError(int errorCode, IntPtr errorPtr)
        {
            if (errorCode == 0) // Success
                return;

            try
            {
                var errorInfo = MarshalError(errorPtr);
                throw new ChromaException(errorInfo);
            }
            finally
            {
                if (errorPtr != IntPtr.Zero)
                    NativeMethods.chroma_free_error(errorPtr);
            }
        }

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
                out _handle,
                out var errorPtr);

            CheckError(result, errorPtr);
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
            return CreateOrGetCollection(name, false, embeddingFunction, metadata);
        }

        /// <summary>
        /// Creates a new collection or gets an existing one
        /// </summary>
        /// <param name="name">Collection name</param>
        /// <param name="embeddingFunction">Function to generate embeddings</param>
        /// <param name="metadata">Collection metadata</param>
        /// <returns>A Collection object</returns>
        public Collection CreateOrGetCollection(string name, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)
        {
            return CreateOrGetCollection(name, true, embeddingFunction, metadata);
        }

        /// <summary>
        /// Creates a new collection or gets an existing one
        /// </summary>
        /// <param name="name">Collection name</param>
        /// <param name="getOrCreate">Whether to get an existing collection if it exists</param>
        /// <param name="embeddingFunction">Function to generate embeddings</param>
        /// <param name="metadata">Collection metadata</param>
        /// <returns>A Collection object</returns>
        private Collection CreateOrGetCollection(string name, bool getOrCreate, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)
        {
            string configJson = null;
            if (embeddingFunction != null)
            {
                // The Rust core expects the configuration JSON to directly represent
                // the embedding function's config, including its 'type'.
                // We should serialize the Configuration object directly, not wrap it.
                configJson = JsonSerializer.Serialize(embeddingFunction.Configuration);
            }

            string metadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null;

            var result = NativeMethods.chroma_create_collection(
                _handle,
                name,
                configJson, // Pass the direct configuration JSON
                metadataJson,
                getOrCreate,
                null,
                null,
                out var collectionHandle,
                out var errorPtr);

            CheckError(result, errorPtr);

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
                out var collectionHandle,
                out var errorPtr);

            CheckError(result, errorPtr);

            return new Collection(this, collectionHandle, embeddingFunction);
        }

        /// <summary>
        /// Gets or creates a collection with the specified name
        /// </summary>
        /// <param name="name">Collection name</param>
        /// <param name="embeddingFunction">Function to generate embeddings</param>
        /// <param name="metadata">Collection metadata (only used if creating)</param>
        /// <returns>A Collection object</returns>
        public Collection GetOrCreateCollection(string name, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)
        {
            try
            {
                return GetCollection(name, embeddingFunction);
            }
            catch (ChromaException)
            {
                return CreateCollection(name, embeddingFunction, metadata);
            }
        }

        /// <summary>
        /// Gets a heartbeat from the server
        /// </summary>
        /// <returns>Current timestamp</returns>
        public ulong Heartbeat()
        {
            var result = NativeMethods.chroma_heartbeat(_handle, out var timestamp, out var errorPtr);

            CheckError(result, errorPtr);

            return timestamp;
        }

        /// <summary>
        /// Creates a database in ChromaDB
        /// </summary>
        /// <param name="name">Database name</param>
        /// <param name="tenant">Optional tenant name</param>
        public void CreateDatabase(string name, string tenant = null)
        {
            var result = NativeMethods.chroma_create_database(
                _handle,
                name,
                tenant,
                out var errorPtr);

            CheckError(result, errorPtr);
        }

        /// <summary>
        /// Gets a database ID
        /// </summary>
        /// <param name="name">Database name</param>
        /// <param name="tenant">Optional tenant name</param>
        /// <returns>Database ID</returns>
        public string GetDatabaseId(string name, string tenant = null)
        {
            var result = NativeMethods.chroma_get_database(
                _handle,
                name,
                tenant,
                out var idPtr,
                out var errorPtr);

            CheckError(result, errorPtr);

            try
            {
                return Marshal.PtrToStringAnsi(idPtr);
            }
            finally
            {
                NativeMethods.chroma_free_string(idPtr);
            }
        }

        /// <summary>
        /// Deletes a database
        /// </summary>
        /// <param name="name">Database name</param>
        /// <param name="tenant">Optional tenant name</param>
        public void DeleteDatabase(string name, string tenant = null)
        {
            var result = NativeMethods.chroma_delete_database(
                _handle,
                name,
                tenant,
                out var errorPtr);

            CheckError(result, errorPtr);
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
                    var result = NativeMethods.chroma_destroy_client(_handle, out var errorPtr);

                    // We don't throw exceptions in Dispose, but we should at least log any errors
                    if (result != 0 && errorPtr != IntPtr.Zero)
                    {
                        try
                        {
                            var errorInfo = MarshalError(errorPtr);
                            Console.Error.WriteLine($"Error disposing ChromaClient: {errorInfo}");
                        }
                        finally
                        {
                            NativeMethods.chroma_free_error(errorPtr);
                        }
                    }

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
        /// Gets the number of documents in the collection
        /// </summary>
        /// <returns>The document count</returns>
        public uint Count()
        {
            var result = NativeMethods.chroma_count(
                _client.Handle,
                _handle,
                out uint count);

            if (result != 0)
                throw new ChromaException($"Failed to get count: {result}");

            return count;
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
                    textsPtr,
                    out var errorPtr);

                ChromaClient.CheckError(result, errorPtr);
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
        /// Updates existing documents in the collection
        /// </summary>
        /// <param name="documents">Documents to update</param>
        public void Update(IEnumerable<ChromaDocument> documents)
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
            var embeddingDim = docs[0].Embedding != null ? docs[0].Embedding.Length : 0;
            var embeddingsPtr = IntPtr.Zero;

            if (embeddingDim > 0)
            {
                embeddingsPtr = MarshalEmbeddings(docs.Select(d => d.Embedding).ToArray());
            }

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
                var result = NativeMethods.chroma_update(
                    _client.Handle,
                    _handle,
                    idsPtr,
                    (UIntPtr)ids.Length,
                    embeddingsPtr,
                    (UIntPtr)embeddingDim,
                    metadataPtr,
                    textsPtr);

                if (result != 0)
                    throw new ChromaException($"Failed to update documents: {result}");
            }
            catch (ChromaException ex)
            {
                // Handle specific update errors if needed
                throw new ChromaException($"Failed to update documents: {ex.Message}");
            }
            finally
            {
                FreeStringArray(idsPtr, ids.Length);
                FreeStringArray(metadataPtr, metadataJsons.Length);
                FreeStringArray(textsPtr, texts.Length);
                if (embeddingsPtr != IntPtr.Zero)
                    FreeEmbeddings(embeddingsPtr, docs.Count);
            }
        }

        /// <summary>
        /// Upserts documents (insert if not present, update if present)
        /// </summary>
        /// <param name="documents">Documents to upsert</param>
        public void Upsert(IEnumerable<ChromaDocument> documents)
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
                var result = NativeMethods.chroma_upsert(
                    _client.Handle,
                    _handle,
                    idsPtr,
                    (UIntPtr)ids.Length,
                    embeddingsPtr,
                    (UIntPtr)embeddingDim,
                    metadataPtr,
                    textsPtr);

                if (result != 0)
                    throw new ChromaException($"Failed to upsert documents: {result}");
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
        /// Deletes documents from the collection
        /// </summary>
        /// <param name="ids">Optional list of document IDs to delete</param>
        /// <param name="whereFilter">Optional metadata filter</param>
        /// <param name="whereDocument">Optional document content filter</param>
        public void Delete(
            IEnumerable<string>? ids = null,
            Dictionary<string, object>? whereFilter = null,
            string? whereDocument = null)
        {
            // At least one of the parameters must be provided
            if (ids == null && whereFilter == null && whereDocument == null)
                throw new ArgumentException("You must provide at least one of: ids, whereFilter, or whereDocument");

            // Marshal IDs
            IntPtr idsPtr = IntPtr.Zero;
            UIntPtr idsCount = UIntPtr.Zero;

            if (ids != null)
            {
                var idsList = ids.ToList();
                if (idsList.Count > 0)
                {
                    idsPtr = MarshalStringArray(idsList.ToArray());
                    idsCount = (UIntPtr)idsList.Count;
                }
            }

            // Convert whereFilter to JSON
            string whereFilterJson = whereFilter != null ? JsonSerializer.Serialize(whereFilter) : null;

            try
            {
                var result = NativeMethods.chroma_delete(
                    _client.Handle,
                    _handle,
                    idsPtr,
                    idsCount,
                    whereFilterJson,
                    whereDocument);

                if (result != 0)
                    throw new ChromaException($"Failed to delete documents: {result}");
            }
            finally
            {
                if (idsPtr != IntPtr.Zero)
                {
                    FreeStringArray(idsPtr, (int)idsCount.ToUInt64());
                }
            }
        }

        /// <summary>
        /// Gets documents from the collection
        /// </summary>
        /// <param name="ids">Optional list of document IDs to retrieve</param>
        /// <param name="whereFilter">Optional metadata filter</param>
        /// <param name="whereDocument">Optional document content filter</param>
        /// <param name="limit">Maximum number of documents to return</param>
        /// <param name="offset">Number of documents to skip</param>
        /// <param name="includeEmbeddings">Include embeddings in results</param>
        /// <param name="includeMetadatas">Include metadata in results</param>
        /// <param name="includeDocuments">Include document text in results</param>
        /// <returns>Query results</returns>
        public QueryResult Get(
            IEnumerable<string>? ids = null,
            Dictionary<string, object>? whereFilter = null,
            string? whereDocument = null,
            uint limit = 0,
            uint offset = 0,
            bool includeEmbeddings = false,
            bool includeMetadatas = true,
            bool includeDocuments = true)
        {
            // Marshal IDs
            IntPtr idsPtr = IntPtr.Zero;
            UIntPtr idsCount = UIntPtr.Zero;

            if (ids != null)
            {
                var idsList = ids.ToList();
                if (idsList.Count > 0)
                {
                    idsPtr = MarshalStringArray(idsList.ToArray());
                    idsCount = (UIntPtr)idsList.Count;
                }
            }

            // Convert whereFilter to JSON
            string whereFilterJson = whereFilter != null ? JsonSerializer.Serialize(whereFilter) : null;

            try
            {
                var result = NativeMethods.chroma_get(
                    _client.Handle,
                    _handle,
                    idsPtr,
                    idsCount,
                    whereFilterJson,
                    whereDocument,
                    limit,
                    offset,
                    includeEmbeddings,
                    includeMetadatas,
                    includeDocuments,
                    out var queryResultPtr);

                if (result != 0)
                    throw new ChromaException($"Failed to get documents: {result}");

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
                if (idsPtr != IntPtr.Zero)
                {
                    FreeStringArray(idsPtr, (int)idsCount.ToUInt64());
                }
            }
        }

        /// <summary>
        /// Gets a document by ID
        /// </summary>
        /// <param name="id">Document ID</param>
        /// <param name="includeEmbedding">Include embedding in result</param>
        /// <returns>The document or null if not found</returns>
        public ChromaDocument GetById(string id, bool includeEmbedding = false)
        {
            return Get(
                ids: new[] { id },
                includeEmbeddings: includeEmbedding,
                includeMetadatas: true,
                includeDocuments: true
            ).FirstOrDefault();
        }

        /// <summary>
        /// Gets documents matching the specified filter
        /// </summary>
        /// <param name="filter">Filter to apply</param>
        /// <param name="limit">Maximum number of results</param>
        /// <param name="offset">Number of results to skip</param>
        /// <param name="includeEmbeddings">Include embeddings in results</param>
        /// <returns>Query results</returns>
        public QueryResult Where(WhereFilter filter, uint limit = 0, uint offset = 0, bool includeEmbeddings = false)
        {
            return Get(
                whereFilter: filter.ToDictionary(),
                limit: limit,
                offset: offset,
                includeEmbeddings: includeEmbeddings
            );
        }

        /// <summary>
        /// Gets documents matching the specified filter
        /// </summary>
        /// <param name="field">Field to filter on</param>
        /// <param name="value">Value to match</param>
        /// <param name="limit">Maximum number of results</param>
        /// <param name="offset">Number of results to skip</param>
        /// <param name="includeEmbeddings">Include embeddings in results</param>
        /// <returns>Query results</returns>
        public QueryResult Where(string field, object value, uint limit = 0, uint offset = 0, bool includeEmbeddings = false)
        {
            var filter = new WhereFilter().Equals(field, value);
            return Where(filter, limit, offset, includeEmbeddings);
        }

        /// <summary>
        /// Executes a semantic search using text
        /// </summary>
        /// <param name="text">Text to search for</param>
        /// <param name="limit">Maximum number of results</param>
        /// <param name="filter">Optional filter to apply</param>
        /// <returns>Query results</returns>
        public QueryResult Search(string text, int limit = 10, WhereFilter filter = null)
        {
            return Query(
                queryText: text,
                nResults: limit,
                whereFilter: filter?.ToDictionary()
            );
        }

        /// <summary>
        /// Executes a semantic search with filtering
        /// </summary>
        /// <param name="text">Text to search for</param>
        /// <param name="field">Field to filter on</param>
        /// <param name="value">Value to match</param>
        /// <param name="limit">Maximum number of results</param>
        /// <returns>Query results</returns>
        public QueryResult Search(string text, string field, object value, int limit = 10)
        {
            var filter = new WhereFilter().Equals(field, value);
            return Search(text, limit, filter);
        }

        /// <summary>
        /// Adds a single document to the collection
        /// </summary>
        /// <param name="document">Document to add</param>
        public void Add(ChromaDocument document)
        {
            Add(new[] { document });
        }

        /// <summary>
        /// Adds a document to the collection
        /// </summary>
        /// <param name="id">Document ID</param>
        /// <param name="text">Document text</param>
        /// <param name="metadata">Optional metadata</param>
        public void Add(string id, string text, Dictionary<string, object> metadata = null)
        {
            Add(ChromaDocument.Create(id, text, metadata));
        }

        /// <summary>
        /// Updates a single document in the collection
        /// </summary>
        /// <param name="document">Document to update</param>
        public void Update(ChromaDocument document)
        {
            Update(new[] { document });
        }

        /// <summary>
        /// Updates a document in the collection
        /// </summary>
        /// <param name="id">Document ID</param>
        /// <param name="text">New document text</param>
        /// <param name="metadata">New metadata (or null to leave unchanged)</param>
        public void Update(string id, string text, Dictionary<string, object> metadata = null)
        {
            Update(ChromaDocument.Create(id, text, metadata));
        }

        /// <summary>
        /// Upserts a single document in the collection
        /// </summary>
        /// <param name="document">Document to upsert</param>
        public void Upsert(ChromaDocument document)
        {
            Upsert(new[] { document });
        }

        /// <summary>
        /// Upserts a document in the collection
        /// </summary>
        /// <param name="id">Document ID</param>
        /// <param name="text">Document text</param>
        /// <param name="metadata">Optional metadata</param>
        public void Upsert(string id, string text, Dictionary<string, object> metadata = null)
        {
            Upsert(ChromaDocument.Create(id, text, metadata));
        }

        /// <summary>
        /// Deletes a document by ID
        /// </summary>
        /// <param name="id">Document ID</param>
        public void Delete(string id)
        {
            Delete(new[] { id });
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
                    var result = NativeMethods.chroma_destroy_collection(_handle, out var errorPtr);

                    // We don't throw exceptions in Dispose, but we should at least log any errors
                    if (result != 0 && errorPtr != IntPtr.Zero)
                    {
                        try
                        {
                            var errorInfo = ChromaClient.MarshalError(errorPtr);
                            Console.Error.WriteLine($"Error disposing ChromaCollection: {errorInfo}");
                        }
                        finally
                        {
                            NativeMethods.chroma_free_error(errorPtr);
                        }
                    }

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