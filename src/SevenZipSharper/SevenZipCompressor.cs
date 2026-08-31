using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using Microsoft.Extensions.Logging;
using SevenZipSharper.Compression;
using SevenZipSharper.Extraction;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;
using static SevenZipSharper.Compression.SevenZipCompressorLog;

namespace SevenZipSharper
{
    /// <summary>
    /// Creates and modifies 7-Zip-compatible archives.
    /// </summary>
    /// <remarks>Dispose when done — the underlying native archive object is released in <see cref="Dispose"/>.</remarks>
    public sealed class SevenZipCompressor : IDisposable
    {
        private readonly ArchiveFormat _format;
        private readonly CompressionParameters _parameters;
        private readonly ILogger<SevenZipCompressor> _logger;
        private readonly IOutArchive _archive;
        private readonly bool _applyNativeParameters;
        private int _disposed;

        /// <summary>
        /// Initializes a new compressor for the given format and parameters.
        /// </summary>
        /// <param name="format">Archive format to create.</param>
        /// <param name="parameters">Compression parameters applied to the archive.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        [ExcludeFromCodeCoverage(
            Justification = "Loads the native 7-Zip library and creates a native COM archive object; exercised end-to-end by the integration test matrix."
        )]
        public SevenZipCompressor(ArchiveFormat format, CompressionParameters parameters, ILogger<SevenZipCompressor> logger)
        {
            NativeLibraryLoader.Register();
            _format = format;
            _parameters = parameters;
            _logger = logger;
            _archive = SevenZipLib.CreateArchiveObject<IOutArchive>(ArchiveFormatRegistry.GetClassId(format));
            _applyNativeParameters = true;
        }

        // For unit testing — bypasses native library creation and parameter application.
        internal SevenZipCompressor(ArchiveFormat format, CompressionParameters parameters, IOutArchive archive, ILogger<SevenZipCompressor> logger)
        {
            _format = format;
            _parameters = parameters;
            _archive = archive;
            _logger = logger;
            _applyNativeParameters = false;
        }

        /// <summary>
        /// Creates a new compressor, returning <c>Result.Fail</c> if the native library cannot be loaded.
        /// </summary>
        /// <param name="format">Archive format to create.</param>
        /// <param name="parameters">Compression parameters applied to the archive.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <returns>A successful result containing the compressor, or a failed result with the error message.</returns>
        [ExcludeFromCodeCoverage(
            Justification = "Thin try/catch around the native-constructing public constructor; exercised end-to-end by the integration test matrix."
        )]
        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Factory intentionally maps any native-load failure to Result.Fail per the Result<T> expected-failure convention."
        )]
        [SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "Ownership of the compressor transfers to the caller via Result<T>; disposing here would return a dead object."
        )]
        public static Result<SevenZipCompressor> Create(ArchiveFormat format, CompressionParameters parameters, ILogger<SevenZipCompressor> logger)
        {
            try
            {
                return Result.Ok(new SevenZipCompressor(format, parameters, logger));
            }
            catch (Exception ex)
            {
                return Result.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Compresses the provided in-memory entries into a single archive stream.
        /// </summary>
        /// <param name="entries">Entries to compress; each is a tuple of the archive-relative path and a readable data stream.</param>
        /// <param name="output">Writable stream that receives the archive data.</param>
        /// <param name="progress">Optional progress sink; receives a snapshot after each block of bytes is processed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A successful result on completion, or a failed result if compression fails.</returns>
        /// <example>
        /// <code>
        /// using var compressor = new SevenZipCompressor(
        ///     ArchiveFormat.SevenZip, CompressionParameters.MaximumLzma2, logger);
        /// var entries = new[]
        /// {
        ///     ("readme.md", (Stream)new MemoryStream(readmeBytes)),
        ///     ("data.bin",  (Stream)new MemoryStream(dataBytes)),
        /// };
        /// using var output = File.Create("archive.7z");
        /// await compressor.CompressAsync(entries, output);
        /// </code>
        /// </example>
        /// <exception cref="ObjectDisposedException">Thrown if the compressor has been disposed.</exception>
        public async Task<Result> CompressAsync(
            IEnumerable<(string EntryPath, Stream Data)> entries,
            Stream output,
            IProgress<CompressionProgress>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            var entryList = entries.ToList();
            return await CompressInternalAsync(entryList, output, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Compresses files from disk into a single archive stream.
        /// </summary>
        /// <param name="filePaths">Absolute paths of the files to compress.</param>
        /// <param name="basePath">Root directory used to compute archive-relative entry paths via <see cref="Path.GetRelativePath"/>.</param>
        /// <param name="output">Writable stream that receives the archive data.</param>
        /// <param name="progress">Optional progress sink; receives a snapshot after each block of bytes is processed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A successful result on completion, or a failed result if compression fails.</returns>
        /// <remarks>Each file is opened lazily when the native compressor requests its stream, so at most one file handle is held open at a time.</remarks>
        /// <example>
        /// <code>
        /// var files = Directory.GetFiles("/var/data", "*.log", SearchOption.AllDirectories);
        /// using var output = File.Create("logs.7z");
        /// await compressor.CompressFilesAsync(files, basePath: "/var/data", output);
        /// </code>
        /// </example>
        /// <exception cref="ObjectDisposedException">Thrown if the compressor has been disposed.</exception>
        public async Task<Result> CompressFilesAsync(
            IEnumerable<string> filePaths,
            string basePath,
            Stream output,
            IProgress<CompressionProgress>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            var lazyStreams = new List<LazyFileStream>();
            try
            {
                var entries = filePaths
                    .Select(path =>
                    {
                        var lazy = new LazyFileStream(path);
                        lazyStreams.Add(lazy);
                        return (EntryPath: Path.GetRelativePath(basePath, path), Data: (Stream)lazy);
                    })
                    .ToList();

                return await CompressInternalAsync(entries, output, progress, cancellationToken, ownsEntryStreams: true).ConfigureAwait(false);
            }
            finally
            {
                foreach (var lazy in lazyStreams)
                    await lazy.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Compresses entries into a split multi-volume archive.
        /// </summary>
        /// <param name="entries">Entries to compress; each is a tuple of the archive-relative path and a readable data stream.</param>
        /// <param name="volumeStreamFactory">Factory called with the zero-based volume index; must return a writable stream for that volume.</param>
        /// <param name="maxVolumeBytes">Maximum size in bytes for each volume.</param>
        /// <param name="progress">Optional progress sink; receives a snapshot after each block of bytes is processed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A successful result on completion, or a failed result if compression fails.</returns>
        /// <example>
        /// <code>
        /// // 50 MB volumes named archive.7z.001, .002, ...
        /// Stream VolumeFactory(int index) =>
        ///     File.Create($"archive.7z.{(index + 1):D3}");
        /// await compressor.CompressMultiVolumeAsync(
        ///     entries, VolumeFactory, maxVolumeBytes: 50UL * 1024 * 1024);
        /// </code>
        /// </example>
        /// <exception cref="ObjectDisposedException">Thrown if the compressor has been disposed.</exception>
        public async Task<Result> CompressMultiVolumeAsync(
            IEnumerable<(string EntryPath, Stream Data)> entries,
            Func<int, Stream> volumeStreamFactory,
            ulong maxVolumeBytes,
            IProgress<CompressionProgress>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            ArgumentNullException.ThrowIfNull(volumeStreamFactory);
            var entryList = entries.ToList();
            return await CompressMultiVolumeInternalAsync(entryList, volumeStreamFactory, maxVolumeBytes, progress, cancellationToken).ConfigureAwait(false);
        }

        // For unit testing — bypasses native library and parameter application.
        internal Task<Result> CompressMultiVolumeAsync(
            IOutArchive archive,
            IEnumerable<(string EntryPath, Stream Data)> entries,
            Func<int, Stream> volumeStreamFactory,
            ulong maxVolumeBytes,
            IProgress<CompressionProgress>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            var entryList = entries.ToList();
            var handler = new MultiVolumeCompressionHandler(
                entryList,
                progress,
                volumeStreamFactory,
                maxVolumeBytes,
                cancellationToken,
                password: _parameters.EncryptionPassword
            );
            var firstVolume = new OutStreamAdapter(volumeStreamFactory(0));
            return RunUpdateItemsAsync(archive, firstVolume, (uint)entryList.Count, handler, cancellationToken);
        }

        /// <summary>
        /// Appends new entries to an existing archive, writing the result to a new stream.
        /// </summary>
        /// <param name="existingArchive">Readable stream of the existing archive to read from.</param>
        /// <param name="newEntries">New entries to append; each is a tuple of the archive-relative path and a readable data stream.</param>
        /// <param name="output">Writable stream that receives the combined archive data.</param>
        /// <param name="progress">Optional progress sink; receives a snapshot after each block of bytes is processed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A successful result on completion, or a failed result if the operation fails or the format does not support append.</returns>
        /// <remarks>
        /// If the existing archive is encrypted, <see cref="CompressionParameters.EncryptionPassword"/>
        /// must match the password the existing archive was created with. A mismatched password
        /// produces an archive whose existing and appended entries cannot be decrypted with the
        /// same password — there is no detection for this case.
        /// </remarks>
        /// <example>
        /// <code>
        /// using var existing = File.OpenRead("archive.7z");
        /// using var output   = File.Create("archive-updated.7z");
        /// var newEntries = new[]
        /// {
        ///     ("changelog.txt", (Stream)new MemoryStream(changelogBytes)),
        /// };
        /// await compressor.AppendAsync(existing, newEntries, output);
        /// </code>
        /// </example>
        /// <exception cref="ObjectDisposedException">Thrown if the compressor has been disposed.</exception>
        public async Task<Result> AppendAsync(
            Stream existingArchive,
            IEnumerable<(string EntryPath, Stream Data)> newEntries,
            Stream output,
            IProgress<CompressionProgress>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            var validation = ValidateAll();
            if (validation.IsFailed)
                return validation;

            var newEntryList = newEntries.ToList();

            var inArchive = SevenZipLib.CreateArchiveObject<IInArchive>(ArchiveFormatRegistry.GetClassId(_format));

            try
            {
                var openHr = inArchive.Open(new InStreamAdapter(existingArchive), IntPtr.Zero, new ArchiveOpenHandler(_parameters.EncryptionPassword));
                if (openHr != HResult.Ok)
                {
                    OpenExistingFailed(_logger, openHr);
                    return Result.Fail($"Failed to open existing archive (HRESULT: 0x{openHr:X8}).");
                }

                inArchive.GetNumberOfItems(out var existingCount);

                if (inArchive is not IOutArchive outArchive)
                {
                    AppendNotSupported(_logger, _format);
                    return Result.Fail("Archive format does not support append.");
                }

                var applyResult = ApplyParametersTo(outArchive);
                if (applyResult.IsFailed)
                    return applyResult;

                return await AppendCoreAsync(inArchive, outArchive, existingCount, newEntryList, output, progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                inArchive.Close();
            }
        }

        // For unit testing — bypasses native library open and QueryInterface.
        internal Task<Result> AppendAsync(
            IInArchive inArchive,
            IOutArchive outArchive,
            uint existingCount,
            IEnumerable<(string EntryPath, Stream Data)> newEntries,
            Stream output,
            IProgress<CompressionProgress>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            var validation = ValidateAll();
            if (validation.IsFailed)
                return Task.FromResult(validation);

            return AppendCoreAsync(inArchive, outArchive, existingCount, newEntries.ToList(), output, progress, cancellationToken);
        }

        /// <summary>
        /// Releases the underlying native archive object.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            if (_archive is IDisposable disposable)
                disposable.Dispose();
        }

        private async Task<Result> CompressInternalAsync(
            List<(string EntryPath, Stream Data)> entries,
            Stream output,
            IProgress<CompressionProgress>? progress,
            CancellationToken cancellationToken,
            bool ownsEntryStreams = false
        )
        {
            var validation = ValidateAll();
            if (validation.IsFailed)
                return validation;

            if (_applyNativeParameters)
            {
                var applyResult = ApplyParametersTo(_archive);
                if (applyResult.IsFailed)
                    return applyResult;
            }

            var outAdapter = new OutStreamAdapter(output);
            var handler = new CompressionHandler(entries, progress, cancellationToken, ownsEntryStreams, password: _parameters.EncryptionPassword);

            var result = await RunUpdateItemsAsync(_archive, outAdapter, (uint)entries.Count, handler, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
                CompressionCompleted(_logger, (uint)entries.Count, _format);

            return result;
        }

        private async Task<Result> CompressMultiVolumeInternalAsync(
            List<(string EntryPath, Stream Data)> entries,
            Func<int, Stream> volumeStreamFactory,
            ulong maxVolumeBytes,
            IProgress<CompressionProgress>? progress,
            CancellationToken cancellationToken
        )
        {
            var validation = ValidateAll();
            if (validation.IsFailed)
                return validation;

            if (_applyNativeParameters)
            {
                var applyResult = ApplyParametersTo(_archive);
                if (applyResult.IsFailed)
                    return applyResult;
            }

            var handler = new MultiVolumeCompressionHandler(
                entries,
                progress,
                volumeStreamFactory,
                maxVolumeBytes,
                cancellationToken,
                password: _parameters.EncryptionPassword
            );
            var firstVolume = new OutStreamAdapter(volumeStreamFactory(0));

            var result = await RunUpdateItemsAsync(_archive, firstVolume, (uint)entries.Count, handler, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
                MultiVolumeCompleted(_logger, _format, (uint)entries.Count);

            return result;
        }

        private async Task<Result> AppendCoreAsync(
            IInArchive inArchive,
            IOutArchive outArchive,
            uint existingCount,
            List<(string EntryPath, Stream Data)> newEntries,
            Stream output,
            IProgress<CompressionProgress>? progress,
            CancellationToken cancellationToken
        )
        {
            var handler = new AppendUpdateHandler(inArchive, existingCount, newEntries, progress, cancellationToken, password: _parameters.EncryptionPassword);
            var outAdapter = new OutStreamAdapter(output);
            var totalCount = existingCount + (uint)newEntries.Count;

            var result = await RunUpdateItemsAsync(outArchive, outAdapter, totalCount, handler, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
                AppendCompleted(_logger, _format, existingCount, newEntries.Count);

            return result;
        }

        private Task<Result> RunUpdateItemsAsync(
            IOutArchive archive,
            IOutStream outStream,
            uint count,
            IArchiveUpdateCallback handler,
            CancellationToken cancellationToken
        ) =>
            Task.Run(
                () =>
                {
                    var hr = archive.UpdateItems(outStream, count, handler);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (hr == HResult.Ok)
                        return Result.Ok();

                    CompressionFailed(_logger, _format, hr);
                    return Result.Fail($"Compression failed (HRESULT: 0x{hr:X8}).");
                },
                cancellationToken
            );

        private Result ValidateAll()
        {
            var paramResult = _parameters.Validate();
            if (paramResult.IsFailed)
                return paramResult;

            return ValidateFormatCompatibility();
        }

        private Result ValidateFormatCompatibility()
        {
            if (_parameters.EncryptHeaders && _format != ArchiveFormat.SevenZip)
                return Result.Fail("EncryptHeaders is only supported for the 7z format.");

            if (_parameters.EncryptionPassword is not null && _format is not ArchiveFormat.SevenZip and not ArchiveFormat.Zip)
            {
                return Result.Fail("Encryption is only supported for the 7z and Zip formats.");
            }

            return Result.Ok();
        }

        [ExcludeFromCodeCoverage(
            Justification = "Performs unsafe pointer pinning and raw-pointer ISetProperties marshalling; unit-testing would require mocking the unmanaged boundary. Exercised by every non-default-parameter integration test on all three OSes."
        )]
        private Result ApplyParametersTo(IOutArchive archive)
        {
            // Zip does not support LZMA2 — fall back to Deflate.
            // NOTE: Other single-codec formats (Tar, GZip, BZip2, Xz) also need compatible method
            // mappings here; audit them before adding support for those formats.
            var parameters =
                _format == ArchiveFormat.Zip && _parameters.Method == CompressionMethod.Lzma2
                    ? _parameters with
                    {
                        Method = CompressionMethod.Deflate,
                    }
                    : _parameters;
            var (names, values) = CompressionParametersMapper.ToSetProperties(parameters, _format);
            if (names.Length == 0)
                return Result.Ok();

            var namePointers = new nint[names.Length];
            try
            {
                for (var i = 0; i < names.Length; i++)
                    namePointers[i] = SevenZipWideString.Alloc(names[i]);

                if (archive is ISetProperties setProps)
                {
                    var nameHandle = GCHandle.Alloc(namePointers, GCHandleType.Pinned);
                    try
                    {
                        // Windows propidlbase.h PROPVARIANT is 24 bytes on x64 (8-byte header +
                        // 16-byte inner union — the inner union must fit counted-array members
                        // like CAUB = {ULONG cElems; XXX *pElems;} which pad to 16 on x64).
                        // POSIX 7-Zip uses a simpler PROPVARIANT (8 + 8 = 16 bytes) per
                        // CPP/Common/MyWindows.h. Our managed PropVariant is 16 bytes to match
                        // POSIX; on Windows we must repack into a 24-byte-stride buffer or
                        // ISetProperties reads the wrong VARTYPE for values[1..] and returns
                        // E_INVALIDARG.
                        var (valuesPtr, freeValues) = PropVariantMarshaller.AllocValuesBuffer(values);
                        try
                        {
                            var hr = setProps.SetProperties(nameHandle.AddrOfPinnedObject(), valuesPtr, (uint)names.Length);
                            if (hr != HResult.Ok)
                            {
                                SetPropertiesFailed(_logger, hr);
                                return Result.Fail($"Failed to apply compression parameters (HRESULT: 0x{hr:X8}).");
                            }
                        }
                        finally
                        {
                            freeValues();
                        }
                    }
                    finally
                    {
                        nameHandle.Free();
                    }
                }
            }
            finally
            {
                foreach (var p in namePointers.Where(p => p != 0))
                    SevenZipWideString.Free(p);
                for (var i = 0; i < values.Length; i++)
                    values[i].Clear();
            }

            return Result.Ok();
        }

        // Opens the underlying FileStream lazily on the first Seek or Read call so
        // CompressFilesAsync holds at most one file open at a time.
        private sealed class LazyFileStream : Stream
        {
            private readonly string _path;
            private FileStream? _inner;

            internal LazyFileStream(string path)
            {
                _path = path;
            }

            private FileStream Inner => _inner ??= new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => Inner.Length;

            public override long Position
            {
                get => Inner.Position;
                set => Inner.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);

            public override void Flush() => _inner?.Flush();

            [ExcludeFromCodeCoverage(
                Justification = "Defensive throw on a read-only Stream override; never invoked because LazyFileStream is only consumed by 7-Zip's input-stream path."
            )]
            public override void SetLength(long value) => throw new NotSupportedException();

            [ExcludeFromCodeCoverage(
                Justification = "Defensive throw on a read-only Stream override; never invoked because LazyFileStream is only consumed by 7-Zip's input-stream path."
            )]
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _inner?.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
