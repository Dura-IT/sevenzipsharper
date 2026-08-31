using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Interop.Archive
{
    // Vtable: IUnknown (3) → IProgress (2) → IArchiveUpdateCallback (4) → GetVolumeSize/GetVolumeStream
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000600820000")]
    internal partial interface IArchiveUpdateCallback2 : IArchiveUpdateCallback
    {
        [PreserveSig]
        int GetVolumeSize(uint index, nint size);

        [PreserveSig]
        int GetVolumeStream(uint index, out IOutStream? volumeStream);
    }
}
