using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Archive
{
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000600030000")]
    internal partial interface ISetProperties
    {
        // names: const wchar_t * const *  — array of wide-string pointers, count = numProps
        // values: const PROPVARIANT *     — array of PROPVARIANTs, count = numProps
        // Caller pins both arrays and passes raw pointers; see SevenZipCompressor.ApplyParametersTo.
        [PreserveSig]
        int SetProperties(nint names, nint values, uint numProps);
    }
}
