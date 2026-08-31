using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using SevenZipSharper.Interop.Archive;

namespace SevenZipSharper.Compression
{
    [GeneratedComClass]
    internal sealed partial class CompressionHandler : CompressionHandlerBase, IArchiveUpdateCallback
    {
        internal CompressionHandler(
            IReadOnlyList<(string EntryPath, Stream Data)> entries,
            IProgress<CompressionProgress>? progress,
            CancellationToken cancellationToken,
            bool ownsEntryStreams = false,
            string? password = null
        )
            : base(entries, progress, cancellationToken, ownsEntryStreams, password: password) { }
    }
}
