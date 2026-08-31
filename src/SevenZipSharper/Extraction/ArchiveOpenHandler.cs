using System;
using System.Runtime.InteropServices.Marshalling;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;

namespace SevenZipSharper.Extraction
{
    [GeneratedComClass]
    internal sealed partial class ArchiveOpenHandler : IArchiveOpenCallback, IPasswordProvider
    {
        private readonly string? _password;

        internal ArchiveOpenHandler(string? password = null)
        {
            _password = password;
        }

        int IArchiveOpenCallback.SetTotal(IntPtr files, IntPtr bytes) => HResult.Ok;

        int IArchiveOpenCallback.SetCompleted(IntPtr files, IntPtr bytes) => HResult.Ok;

        int IPasswordProvider.GetPassword(out string password)
        {
            password = _password ?? string.Empty;
            return _password is not null ? HResult.Ok : HResult.False;
        }
    }
}
