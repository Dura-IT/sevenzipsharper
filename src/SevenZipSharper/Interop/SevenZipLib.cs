using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop;

[ExcludeFromCodeCoverage(
    Justification = "Thin P/Invoke bridge to the native 7-Zip CreateObject entry point; requires a real native library load to exercise and is covered end-to-end by the integration test matrix. Format CLSIDs live in ArchiveClassIds, which is unit-tested."
)]
internal static partial class SevenZipLib
{
    [LibraryImport(NativeLibraryLoader.LibraryName, EntryPoint = "CreateObject")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int CreateObject(
        in Guid classId,
        in Guid interfaceId,
        out nint outObject
    );

    [SuppressMessage(
        "Security",
        "S6640:Make sure that using \"unsafe\" is safe here.",
        Justification = "Required to cast the 7-Zip CreateObject HRESULT-out pointer through "
            + "ComInterfaceMarshaller<T>.ConvertToManaged, which only accepts void*."
    )]
    internal static unsafe T CreateArchiveObject<T>(Guid classId)
        where T : class
    {
        var interfaceId = typeof(T).GUID;
        var hr = CreateObject(in classId, in interfaceId, out var ptr);

        if (hr != HResult.Ok || ptr == 0)
            throw new InvalidOperationException(
                $"Failed to create 7-Zip archive object (HRESULT: 0x{hr:X8})."
            );

        return ComInterfaceMarshaller<T>.ConvertToManaged((void*)ptr)
            ?? throw new InvalidOperationException("CreateObject returned null.");
    }
}
