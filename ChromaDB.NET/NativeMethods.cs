using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ChromaDB.NET;

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
        out IntPtr result,
        out IntPtr error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int chroma_free_query_result(IntPtr result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int chroma_free_string(IntPtr str);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int chroma_count(
        IntPtr clientHandle,
        IntPtr collectionHandle,
        out uint result,
        out IntPtr error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int chroma_update(
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
    public static extern int chroma_upsert(
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
    public static extern int chroma_delete(
        IntPtr clientHandle,
        IntPtr collectionHandle,
        IntPtr ids,
        UIntPtr idsCount,
        [MarshalAs(UnmanagedType.LPStr)] string whereFilterJson,
        [MarshalAs(UnmanagedType.LPStr)] string whereDocumentFilter,
        out IntPtr error);

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
        out IntPtr result,
        out IntPtr error);

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
