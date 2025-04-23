using System;

namespace ChromaDB.NET;

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