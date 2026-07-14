using System;
using System.Runtime.InteropServices;

namespace SevenZipSharper.Interop;

/// <summary>
/// Cross-platform BSTR allocation, read, and free compatible with 7-Zip's non-Windows runtime.
/// </summary>
/// <remarks>
/// The official 7-Zip SDK (7-zip.org) uses the platform <c>wchar_t</c> for OLECHAR on POSIX.
/// On macOS and Linux, <c>wchar_t</c> is 4 bytes (UCS-4), so BSTR data is stored as 4 bytes
/// per character and <c>byteLen</c> = <c>numChars × 4</c>. Windows uses 2-byte UTF-16 BSTRs
/// via <c>Marshal.StringToBSTR</c> / <c>Marshal.FreeBSTR</c>.
/// The allocation base in all cases is <c>malloc_base</c>; BSTR = <c>malloc_base + 4</c>;
/// <c>SysFreeString</c> does <c>free(bstr - 4)</c>.
/// </remarks>
internal static class SevenZipBStr
{
    /// <summary>
    /// Allocates a BSTR from a managed string.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> exceeds the maximum BSTR length.</exception>
    internal static IntPtr Alloc(string value)
    {
        if (value.Length > int.MaxValue / 4)
        {
            throw new ArgumentException(
                "String is too long to represent as a BSTR.",
                nameof(value)
            );
        }

        if (OperatingSystem.IsWindows())
            return Marshal.StringToBSTR(value);

        // Non-Windows: 7-Zip uses wchar_t (4 bytes) for BSTR data; byteLen = numChars * 4.
        int numChars = value.Length;
        int byteLen = numChars * 4;
        IntPtr ptr = Marshal.AllocCoTaskMem(4 + byteLen + 4); // length header + data + 4-byte null terminator
        Marshal.WriteInt32(ptr, byteLen);
        nint dataPtr = ptr + 4;
        for (int i = 0; i < numChars; i++)
            Marshal.WriteInt32(dataPtr + ((nint)i * 4), value[i]);
        Marshal.WriteInt32(dataPtr + (nint)byteLen, 0);
        return dataPtr;
    }

    /// <summary>
    /// Reads a managed string from a BSTR pointer. Returns <see langword="null"/> for a null or corrupt pointer.
    /// </summary>
    internal static string? Read(IntPtr bstr)
    {
        if (bstr == IntPtr.Zero)
            return null;

        int byteLen = Marshal.ReadInt32(bstr - 4);

        // Guard against corrupt native data returning a nonsensical length.
        if (byteLen < 0 || byteLen > 200_000_000)
            return null;

        if (OperatingSystem.IsWindows())
            return Marshal.PtrToStringUni(bstr, byteLen / 2);

        // Non-Windows: 7-Zip stores BSTR data as wchar_t (4 bytes); byteLen = numChars * 4.
        int numChars = byteLen / 4;
        var chars = new char[numChars];
        for (int i = 0; i < numChars; i++)
            chars[i] = (char)Marshal.ReadInt32(bstr + ((nint)i * 4));
        return new string(chars);
    }

    /// <summary>
    /// Frees a BSTR pointer previously allocated by 7-Zip or by <see cref="Alloc"/>.
    /// </summary>
    internal static void Free(IntPtr bstr)
    {
        if (bstr == IntPtr.Zero)
            return;

        if (OperatingSystem.IsWindows())
        {
            Marshal.FreeBSTR(bstr);
        }
        else
        {
            // On non-Windows, 7-Zip's SysAllocString uses the system malloc, which is the
            // same allocator as CoTaskMem on all supported platforms (libSystem on macOS,
            // glibc on Linux). Free at bstr-4 to match the allocation base.
            Marshal.FreeCoTaskMem(bstr - 4);
        }
    }
}
