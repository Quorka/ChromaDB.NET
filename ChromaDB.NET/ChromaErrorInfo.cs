using System;

namespace ChromaDB.NET;

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
