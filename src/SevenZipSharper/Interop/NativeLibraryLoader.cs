using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace SevenZipSharper.Interop;

internal static class NativeLibraryLoader
{
    internal const string LibraryName = "7z";

    private static int _registered;

    internal static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
            NativeLibrary.SetDllImportResolver(typeof(NativeLibraryLoader).Assembly, Resolve);
    }

    private static IntPtr Resolve(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath
    )
    {
        if (libraryName != LibraryName)
            return IntPtr.Zero;

        // assembly.Location is empty in single-file publish; AppContext.BaseDirectory is always valid.
        var assemblyDir = Path.GetDirectoryName(assembly.Location);
        var baseDir = string.IsNullOrEmpty(assemblyDir) ? AppContext.BaseDirectory : assemblyDir;
        var libraryPath = ResolveLibraryPath(baseDir);
        return NativeLibrary.Load(libraryPath);
    }

    internal static string ResolveLibraryPath(string assemblyDirectory)
    {
        var rid = PlatformInfo.GetRuntimeIdentifier();
        var fileName = PlatformInfo.GetLibraryFileName();
        var path = Path.Combine(assemblyDirectory, "runtimes", rid, "native", fileName);

        if (!File.Exists(path))
        {
            throw new DllNotFoundException(
                $"7-Zip native library not found at '{path}'. "
                    + $"Ensure the SevenZipSharper NuGet package includes native assets for RID '{rid}'."
            );
        }

        return path;
    }
}
