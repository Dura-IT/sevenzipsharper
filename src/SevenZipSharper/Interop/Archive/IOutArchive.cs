using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Interop.Archive
{
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000600A00000")]
    internal partial interface IOutArchive
    {
        [PreserveSig]
        int UpdateItems(IOutStream outStream, uint numItems, IArchiveUpdateCallback updateCallback);

        [PreserveSig]
        int GetFileTimeType(out uint type);
    }
}
