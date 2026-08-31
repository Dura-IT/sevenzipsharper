using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Extraction
{
    // Writes an extracted entry to a new file and owns the underlying FileStream.
    [GeneratedComClass]
    internal sealed partial class FileEntryStream : ISequentialOutStream, IDisposable
    {
        private readonly FileStream _file;
        private readonly IWriteFlushPacer _flushPacer;

        internal FileEntryStream(string fullPath, IWriteFlushPacer flushPacer)
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            _file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            _flushPacer = flushPacer;
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "COM callback must translate every managed exception into an HRESULT; exceptions cannot cross the native 7-Zip boundary."
        )]
        public int Write(byte[] data, uint size, out uint processedSize)
        {
            processedSize = 0;
            try
            {
                _file.Write(data, 0, (int)size);

                // The bytes are accepted once Write returns; record the count before the durability
                // flush so a flush failure still reports what was written rather than zero.
                processedSize = size;

                // Pace decompression to physical disk throughput: force a flush every N bytes so
                // dirty pages cannot accumulate unboundedly ahead of write-back during sustained
                // large-entry extraction. Inside the try so a flush failure surfaces as HResult.Fail.
                if (_flushPacer.ShouldFlush(size))
                    _file.Flush(flushToDisk: true);

                return HResult.Ok;
            }
            catch (Exception)
            {
                return HResult.Fail;
            }
        }

        public void Dispose() => _file.Dispose();
    }
}
