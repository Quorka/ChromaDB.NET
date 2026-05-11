using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace ChromaDB.NET;

/// <summary>
/// Client for interacting with ChromaDB
/// </summary>
public class ChromaClient : IDisposable
{
    private IntPtr _handle;

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
    public ChromaClient(string? persistDirectory = null, int hnswCacheSize = 1000)
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

    public Collection CreateCollection(string name, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)
    {
        return CreateOrGetCollection(name, false, embeddingFunction, metadata);
    }

    public Collection CreateOrGetCollection(string name, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)
    {
        return CreateOrGetCollection(name, true, embeddingFunction, metadata);
    }

    private Collection CreateOrGetCollection(string name, bool getOrCreate, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)
    {
        var handle = GetHandleOrThrow();

        string configJson = null;
        if (embeddingFunction != null)
        {
            configJson = JsonSerializer.Serialize(embeddingFunction.Configuration);
        }

        string metadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null;

        var result = NativeMethods.chroma_create_collection(
            handle,
            name,
            configJson,
            metadataJson,
            getOrCreate,
            null,
            null,
            out var collectionHandle,
            out var errorPtr);

        CheckError(result, errorPtr);

        return new Collection(this, collectionHandle, embeddingFunction);
    }

    public Collection GetCollection(string name, IEmbeddingFunction embeddingFunction = null)
    {
        var handle = GetHandleOrThrow();

        var result = NativeMethods.chroma_get_collection(
            handle,
            name,
            null,
            null,
            out var collectionHandle,
            out var errorPtr);

        CheckError(result, errorPtr);

        return new Collection(this, collectionHandle, embeddingFunction);
    }

    public Collection GetOrCreateCollection(string name, IEmbeddingFunction embeddingFunction = null, Dictionary<string, object> metadata = null)
    {
        try
        {
            return GetCollection(name, embeddingFunction);
        }
        catch (ChromaException ex) when (ex.ErrorInfo.Code == ChromaErrorCode.NotFound)
        {
            return CreateCollection(name, embeddingFunction, metadata);
        }
    }

    public ulong Heartbeat()
    {
        var handle = GetHandleOrThrow();
        var result = NativeMethods.chroma_heartbeat(handle, out var timestamp, out var errorPtr);

        CheckError(result, errorPtr);

        return timestamp;
    }

    public void CreateDatabase(string name, string tenant = null)
    {
        var handle = GetHandleOrThrow();
        var result = NativeMethods.chroma_create_database(
            handle,
            name,
            tenant,
            out var errorPtr);

        CheckError(result, errorPtr);
    }

    public string GetDatabaseId(string name, string tenant = null)
    {
        var handle = GetHandleOrThrow();
        var result = NativeMethods.chroma_get_database(
            handle,
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

    public void DeleteDatabase(string name, string tenant = null)
    {
        var handle = GetHandleOrThrow();
        var result = NativeMethods.chroma_delete_database(
            handle,
            name,
            tenant,
            out var errorPtr);

        CheckError(result, errorPtr);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle == IntPtr.Zero)
            return;

        var result = NativeMethods.chroma_destroy_client(handle, out var errorPtr);

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
    }

    ~ChromaClient()
    {
        Dispose(false);
    }

    internal IntPtr GetHandleOrThrow()
    {
        var handle = Volatile.Read(ref _handle);
        if (handle == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(ChromaClient));
        return handle;
    }
}
