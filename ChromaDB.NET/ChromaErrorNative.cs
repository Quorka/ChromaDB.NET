using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ChromaDB.NET;

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