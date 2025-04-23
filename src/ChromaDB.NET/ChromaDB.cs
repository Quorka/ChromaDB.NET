using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ChromaDB.NET
{

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
                out uint count,
                out var errorPtr);

            ChromaClient.CheckError(result, errorPtr);
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
                    out var queryResultPtr,
                    out var errorPtr);

                ChromaClient.CheckError(result, errorPtr);

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
                    textsPtr,
                    out var errorPtr);

                ChromaClient.CheckError(result, errorPtr);
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
                    whereDocument,
                    out var errorPtr);

                ChromaClient.CheckError(result, errorPtr);
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
                    out var queryResultPtr,
                    out var errorPtr);

                ChromaClient.CheckError(result, errorPtr);

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