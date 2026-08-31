using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Interop.Archive
{
    // Vtable: IUnknown (3) → SetTotal/SetCompleted (IProgress) → GetUpdateItemInfo/GetProperty/GetStream/SetOperationResult
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000600800000")]
    internal partial interface IArchiveUpdateCallback : IProgress
    {
        [PreserveSig]
        int GetUpdateItemInfo(uint index, nint newData, nint newProperties, nint indexInArchive);

        [PreserveSig]
        int GetProperty(uint index, ItemPropId propId, ref PropVariant value);

        [PreserveSig]
        int GetStream(uint index, out ISequentialInStream? inStream);

        [PreserveSig]
        int SetOperationResult(OperationResult result);
    }
}
