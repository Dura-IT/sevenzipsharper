using System;
using System.Runtime.InteropServices;

namespace SevenZipSharper.Interop
{
    /// <summary>
    /// Cross-platform helpers for null-terminated wchar_t string allocation and read compatible with 7-Zip's non-Windows runtime.
    /// </summary>
    /// <remarks>
    /// On Windows, <c>wchar_t</c> is 2 bytes (UTF-16); on POSIX it is 4 bytes (UCS-4).
    /// Use these helpers when crossing the wchar_t boundary with 7-Zip interfaces that take or return
    /// raw <c>const wchar_t*</c> — do NOT use for BSTR parameters (<see cref="SevenZipBStr"/>).
    /// </remarks>
    internal static class SevenZipWideString
    {
        /// <summary>
        /// Allocates a null-terminated wchar_t string from a managed string. Must be freed with <see cref="Free"/>.
        /// </summary>
        internal static nint Alloc(string value)
        {
            if (OperatingSystem.IsWindows())
                return Marshal.StringToCoTaskMemUni(value);

            // POSIX: wchar_t is 4 bytes; allocate (length + 1) * 4 bytes.
            int numChars = value.Length;
            nint ptr = Marshal.AllocCoTaskMem((numChars + 1) * 4);
            for (int i = 0; i < numChars; i++)
                Marshal.WriteInt32(ptr + ((nint)i * 4), value[i]);
            Marshal.WriteInt32(ptr + ((nint)numChars * 4), 0);
            return ptr;
        }

        /// <summary>
        /// Reads a managed string from a null-terminated wchar_t pointer. Returns <see langword="null"/> for a null pointer.
        /// </summary>
        internal static string? Read(nint ptr)
        {
            if (ptr == nint.Zero)
                return null;

            if (OperatingSystem.IsWindows())
                return Marshal.PtrToStringUni(ptr);

            // POSIX: wchar_t is 4 bytes; walk until null terminator.
            int length = 0;
            while (Marshal.ReadInt32(ptr + ((nint)length * 4)) != 0)
                length++;

            var chars = new char[length];
            for (int i = 0; i < length; i++)
                chars[i] = (char)Marshal.ReadInt32(ptr + ((nint)i * 4));
            return new string(chars);
        }

        /// <summary>
        /// Frees a pointer previously allocated by <see cref="Alloc"/>.
        /// </summary>
        internal static void Free(nint ptr) => Marshal.FreeCoTaskMem(ptr);
    }
}
