using System;

namespace SevenZipSharper.Interop
{
    // COM class IDs for 7-Zip archive handler objects.
    // Pattern: 23170F69-40C1-278A-1000-000110{formatId}0000
    internal static class ArchiveClassIds
    {
        internal static readonly Guid SevenZip = new Guid("23170F69-40C1-278A-1000-000110070000");
        internal static readonly Guid Zip = new Guid("23170F69-40C1-278A-1000-000110010000");
        internal static readonly Guid BZip2 = new Guid("23170F69-40C1-278A-1000-000110020000");
        internal static readonly Guid Arj = new Guid("23170F69-40C1-278A-1000-000110040000");
        internal static readonly Guid Lzh = new Guid("23170F69-40C1-278A-1000-000110060000");
        internal static readonly Guid Cab = new Guid("23170F69-40C1-278A-1000-000110080000");
        internal static readonly Guid Iso = new Guid("23170F69-40C1-278A-1000-0001100E0000");
        internal static readonly Guid GZip = new Guid("23170F69-40C1-278A-1000-000110EF0000");
        internal static readonly Guid Tar = new Guid("23170F69-40C1-278A-1000-000110EE0000");
        internal static readonly Guid Xz = new Guid("23170F69-40C1-278A-1000-000110F80000");
        internal static readonly Guid Wim = new Guid("23170F69-40C1-278A-1000-000110E60000");
    }
}
