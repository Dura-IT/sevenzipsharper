using System;
using System.Runtime.InteropServices;

namespace SevenZipSharper.Interop;

internal static class PlatformInfo
{
    internal static string GetRuntimeIdentifier() => BuildRuntimeIdentifier(GetCurrentOS(), RuntimeInformation.ProcessArchitecture);

    internal static string GetLibraryFileName() => BuildLibraryFileName(GetCurrentOS());

    internal static string BuildRuntimeIdentifier(OSPlatform os, Architecture architecture)
    {
        var archSegment = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            // x86 is meaningful only for 32-bit Windows processes (AnyCPU/x86 apps under WOW64
            // on 64-bit Windows); only a win-x86 native ships, but the RID is built generically.
            Architecture.X86 => "x86",
            _ => throw new PlatformNotSupportedException($"Unsupported processor architecture: {architecture}."),
        };

        if (os == OSPlatform.Windows)
            return $"win-{archSegment}";
        if (os == OSPlatform.OSX)
            return $"osx-{archSegment}";
        if (os == OSPlatform.Linux)
            return $"linux-{archSegment}";

        throw new PlatformNotSupportedException($"Unsupported operating system: {os}.");
    }

    internal static string BuildLibraryFileName(OSPlatform os)
    {
        if (os == OSPlatform.Windows)
            return "7z.dll";
        if (os == OSPlatform.OSX)
            return "7z.dylib";
        if (os == OSPlatform.Linux)
            return "7z.so";

        throw new PlatformNotSupportedException($"Unsupported operating system: {os}.");
    }

    private static OSPlatform GetCurrentOS()
    {
        if (OperatingSystem.IsWindows())
            return OSPlatform.Windows;
        if (OperatingSystem.IsMacOS())
            return OSPlatform.OSX;
        if (OperatingSystem.IsLinux())
            return OSPlatform.Linux;

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }
}
