using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop;

/// <summary>
/// Custom marshaller for BSTR parameters in 7-Zip COM interface declarations.
/// </summary>
/// <remarks>
/// Use via <c>[MarshalUsing(typeof(SevenZipBStrMarshaller))]</c> on <c>string</c> parameters
/// in <c>[GeneratedComInterface]</c> declarations. Handles both directions correctly:
/// <list type="bullet">
/// <item>Managed→native <c>out</c>: allocates via <see cref="SevenZipBStr.Alloc"/>; ownership passes to 7-Zip.</item>
/// <item>Native→managed <c>out</c>: reads via <see cref="SevenZipBStr.Read"/>, frees via <see cref="SevenZipBStr.Free"/>.</item>
/// </list>
/// </remarks>
[CustomMarshaller(typeof(string), MarshalMode.Default, typeof(SevenZipBStrMarshaller))]
[SuppressMessage(
    "Security",
    "S6640:Make sure that using \"unsafe\" is safe here.",
    Justification = "Required to marshal raw BSTR pointers across the 7-Zip COM boundary; "
        + "every pointer originates from SevenZipBStr.Alloc/Read and is released via SevenZipBStr.Free."
)]
internal static unsafe class SevenZipBStrMarshaller
{
    /// <summary>
    /// Converts a managed string to an unmanaged BSTR pointer.
    /// </summary>
    public static ushort* ConvertToUnmanaged(string? managed) => managed is null ? null : (ushort*)SevenZipBStr.Alloc(managed);

    /// <summary>
    /// Converts an unmanaged BSTR pointer to a managed string.
    /// </summary>
    public static string? ConvertToManaged(ushort* unmanaged) => SevenZipBStr.Read((nint)unmanaged);

    /// <summary>
    /// Frees an unmanaged BSTR pointer.
    /// </summary>
    public static void Free(ushort* unmanaged) => SevenZipBStr.Free((nint)unmanaged);
}
