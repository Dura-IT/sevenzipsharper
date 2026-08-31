using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop;

[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000500110000")]
internal partial interface ICryptoGetTextPassword2
{
    [PreserveSig]
    int CryptoGetTextPassword2(out int passwordIsDefined, [MarshalUsing(typeof(SevenZipBStrMarshaller))] out string password);
}
