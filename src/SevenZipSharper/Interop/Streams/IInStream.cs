using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Streams
{
    // Vtable: IUnknown (3) → Read (ISequentialInStream) → Seek
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000300030000")]
    internal partial interface IInStream : ISequentialInStream
    {
        [PreserveSig]
        int Seek(long offset, uint seekOrigin, nint newPosition); // UInt64* — nullable; zero means caller does not need the new position
    }
}
