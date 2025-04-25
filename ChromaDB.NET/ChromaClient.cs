using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ChromaDB.NET;

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
            persistDirectory ?? string.Empty,
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
