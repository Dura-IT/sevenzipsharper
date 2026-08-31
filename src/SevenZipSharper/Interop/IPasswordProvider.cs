using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop
{
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000500100000")]
    internal partial interface IPasswordProvider
    {
        [PreserveSig]
        int GetPassword([MarshalUsing(typeof(SevenZipBStrMarshaller))] out string password);
    }
}
