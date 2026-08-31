using System;
using System.Collections.Generic;

namespace SevenZipSharper.Interop;

internal static class ArchiveFormatRegistry
{
    private static readonly Dictionary<ArchiveFormat, Guid> ClassIds = new Dictionary<ArchiveFormat, Guid>
    {
        [ArchiveFormat.SevenZip] = ArchiveClassIds.SevenZip,
        [ArchiveFormat.Zip] = ArchiveClassIds.Zip,
        [ArchiveFormat.BZip2] = ArchiveClassIds.BZip2,
        [ArchiveFormat.Arj] = ArchiveClassIds.Arj,
        [ArchiveFormat.Lzh] = ArchiveClassIds.Lzh,
        [ArchiveFormat.Cab] = ArchiveClassIds.Cab,
        [ArchiveFormat.Iso] = ArchiveClassIds.Iso,
        [ArchiveFormat.GZip] = ArchiveClassIds.GZip,
        [ArchiveFormat.Tar] = ArchiveClassIds.Tar,
        [ArchiveFormat.Xz] = ArchiveClassIds.Xz,
        [ArchiveFormat.Wim] = ArchiveClassIds.Wim,
    };

    /// <summary>
    /// Maps an archive format to the 7-Zip COM class id that handles it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not a recognised value.</exception>
    internal static Guid GetClassId(ArchiveFormat format)
    {
        if (ClassIds.TryGetValue(format, out var classId))
            return classId;

        throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown archive format.");
    }
}
