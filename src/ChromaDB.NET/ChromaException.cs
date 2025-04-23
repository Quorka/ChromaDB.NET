using System;
using System.Collections.Generic;

namespace ChromaDB.NET;
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

