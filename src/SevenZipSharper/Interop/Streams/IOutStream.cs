using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Streams
{
    // Vtable: IUnknown (3) → Write (ISequentialOutStream) → Seek → SetSize
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000300040000")]
    internal partial interface IOutStream : ISequentialOutStream
    {
        [PreserveSig]
        int Seek(long offset, uint seekOrigin, nint newPosition); // UInt64* — nullable; zero means caller does not need the new position

        [PreserveSig]
        int SetSize(ulong newSize);
    }
}
