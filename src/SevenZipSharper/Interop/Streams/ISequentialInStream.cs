using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Streams
{
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000300010000")]
    internal partial interface ISequentialInStream
    {
        [PreserveSig]
        int Read([Out, MarshalUsing(CountElementName = "size")] byte[] data, uint size, out uint processedSize);
    }
}
