using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Interop.Archive;

[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600300000")]
internal partial interface IArchiveOpenVolumeCallback
{
    [PreserveSig]
    int GetProperty(ItemPropId propId, ref PropVariant value);

    [PreserveSig]
    int GetStream([MarshalUsing(typeof(SevenZipWideStringMarshaller))] string name, out IInStream? inStream);
}
