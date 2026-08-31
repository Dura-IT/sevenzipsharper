using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Archive
{
    // SetTotal/SetCompleted take nullable UInt64 pointers — represented as IntPtr.
    [GeneratedComInterface]
    [Guid("23170F69-40C1-278A-0000-000600100000")]
    internal partial interface IArchiveOpenCallback
    {
        [PreserveSig]
        int SetTotal(IntPtr files, IntPtr bytes);

        [PreserveSig]
        int SetCompleted(IntPtr files, IntPtr bytes);
    }
}
