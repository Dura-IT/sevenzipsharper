using System;
using System.Collections.Generic;
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
    internal sealed partial class MultiVolumeCompressionHandler : CompressionHandlerBase, IArchiveUpdateCallback2
    {
        private readonly Func<int, Stream> _volumeStreamFactory;
        private readonly ulong _maxVolumeBytes;

        internal MultiVolumeCompressionHandler(
            IReadOnlyList<(string EntryPath, Stream Data)> entries,
            IProgress<CompressionProgress>? progress,
            Func<int, Stream> volumeStreamFactory,
            ulong maxVolumeBytes,
            CancellationToken cancellationToken,
            string? password = null
        )
            : base(entries, progress, cancellationToken, password: password)
        {
            _volumeStreamFactory = volumeStreamFactory;
            _maxVolumeBytes = maxVolumeBytes;
        }

        int IArchiveUpdateCallback2.GetVolumeSize(uint index, nint size)
        {
            if (size != nint.Zero)
                Marshal.WriteInt64(size, (long)_maxVolumeBytes);
            return HResult.Ok;
        }

        int IArchiveUpdateCallback2.GetVolumeStream(uint index, out IOutStream? volumeStream)
        {
            var stream = _volumeStreamFactory((int)index);
            volumeStream = new OutStreamAdapter(stream);
            return HResult.Ok;
        }
    }
}
