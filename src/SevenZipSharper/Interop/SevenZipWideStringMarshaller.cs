using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop
{
    /// <summary>
    /// Custom marshaller for null-terminated wchar_t string parameters in 7-Zip COM interface declarations.
    /// </summary>
    /// <remarks>
    /// Use via <c>[MarshalUsing(typeof(SevenZipWideStringMarshaller))]</c> on <c>string</c> parameters
    /// in <c>[GeneratedComInterface]</c> declarations where 7-Zip passes a raw <c>const wchar_t*</c>.
    /// Do NOT use this for BSTR parameters; use <see cref="SevenZipBStrMarshaller"/> instead.
    /// </remarks>
    [CustomMarshaller(typeof(string), MarshalMode.Default, typeof(SevenZipWideStringMarshaller))]
    [SuppressMessage(
        "Security",
        "S6640:Make sure that using \"unsafe\" is safe here.",
        Justification = "Required to marshal raw wchar_t pointers across the 7-Zip COM boundary; "
            + "every pointer originates from SevenZipWideString.Alloc/Read and is released via SevenZipWideString.Free."
    )]
    internal static unsafe class SevenZipWideStringMarshaller
    {
        /// <summary>
        /// Converts a managed string to an unmanaged wchar_t pointer.
        /// </summary>
        public static char* ConvertToUnmanaged(string? managed) => managed is null ? null : (char*)SevenZipWideString.Alloc(managed);

        /// <summary>
        /// Converts an unmanaged wchar_t pointer to a managed string.
        /// </summary>
        public static string? ConvertToManaged(char* unmanaged) => SevenZipWideString.Read((nint)unmanaged);

        /// <summary>
        /// Frees an unmanaged wchar_t pointer.
        /// </summary>
        public static void Free(char* unmanaged) => SevenZipWideString.Free((nint)unmanaged);
    }
}
