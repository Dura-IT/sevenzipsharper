using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Interop.Archive
{
    // Vtable: IUnknown (3) → SetTotal/SetCompleted (IProgress) → GetStream/PrepareOperation/SetOperationResult
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000600200000")]
    internal partial interface IArchiveExtractCallback : IProgress
    {
        [PreserveSig]
        int GetStream(uint index, out ISequentialOutStream? outStream, AskMode askExtractMode);

        [PreserveSig]
        int PrepareOperation(AskMode askExtractMode);

        [PreserveSig]
        int SetOperationResult(OperationResult result);
    }
}
