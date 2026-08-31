using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Archive
{
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000000050000")]
    internal partial interface IProgress
    {
        [PreserveSig]
        int SetTotal(ulong total);

        [PreserveSig]
        int SetCompleted(nint completeValue); // const UInt64* — nullable; zero means unavailable
    }
}
