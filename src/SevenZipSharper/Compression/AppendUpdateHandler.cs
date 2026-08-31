using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Compression
{
    [GeneratedComClass]
    internal sealed partial class AppendUpdateHandler : CompressionHandlerBase, IArchiveUpdateCallback
    {
        private readonly IInArchive _existingArchive;

        internal AppendUpdateHandler(
            IInArchive existingArchive,
            uint existingCount,
            IReadOnlyList<(string EntryPath, Stream Data)> newEntries,
            IProgress<CompressionProgress>? progress,
            CancellationToken cancellationToken,
            string? password = null
        )
            : base(newEntries, progress, cancellationToken, existingCount: existingCount, password: password)
        {
            _existingArchive = existingArchive;
        }

        protected override int OnGetExistingUpdateItemInfo(uint index, nint newData, nint newProperties, nint indexInArchive)
        {
            if (newData != nint.Zero)
                Marshal.WriteInt32(newData, 0);
            if (newProperties != nint.Zero)
                Marshal.WriteInt32(newProperties, 0);
            if (indexInArchive != nint.Zero)
                Marshal.WriteInt32(indexInArchive, (int)index);
            return HResult.Ok;
        }

        // `value` is a managed ref into a native-provided buffer (platform-sized PROPVARIANT —
        // 24 bytes on Windows, 16 on POSIX). Forward the raw address to the in-archive's
        // GetProperty(nint) so its write lands directly in that buffer with the correct stride.
        [SuppressMessage(
            "Security",
            "S6640:Make sure that using \"unsafe\" is safe here.",
            Justification = "Required to convert the managed ref-to-PROPVARIANT (which is itself a pointer into the native-provided platform-sized buffer) into a raw nint for the IInArchive.GetProperty(nint) signature. The buffer is owned by outer-native and survives the call. See [[project-interop-gotchas]] round 3."
        )]
        protected override unsafe int OnGetExistingProperty(uint index, ItemPropId propId, ref PropVariant value) =>
            _existingArchive.GetProperty(index, propId, (nint)System.Runtime.CompilerServices.Unsafe.AsPointer(ref value));

        protected override int OnGetExistingStream(uint index, out ISequentialInStream? inStream)
        {
            inStream = null;
            return HResult.Ok;
        }
    }
}
