using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Streams
{
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000300020000")]
    internal partial interface ISequentialOutStream
    {
        [PreserveSig]
        int Write([In, MarshalUsing(CountElementName = "size")] byte[] data, uint size, out uint processedSize);
    }
}
