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
using SevenZipSharper.Extraction;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;
using static SevenZipSharper.Extraction.SevenZipExtractorLog;

namespace SevenZipSharper
{
    /// <summary>
    /// Reads and extracts entries from a 7-Zip-compatible archive.
    /// </summary>
    /// <remarks>
    /// Call <see cref="OpenAsync"/> before calling any other method.
    /// Dispose when done — the underlying native archive object is released in <see cref="Dispose"/>.
    /// </remarks>
    public sealed class SevenZipExtractor : IDisposable
    {
        private readonly ArchiveFormat _format;
        private readonly ILogger<SevenZipExtractor> _logger;
        private readonly IInArchive _archive;
        private readonly InStreamAdapter _streamAdapter;
        private const string NotOpenedMessage = "Call OpenAsync before listing or extracting.";
        private int _disposed;
        private bool _opened;
        private string? _password;

        /// <summary>
        /// Initializes a new extractor for the given stream.
        /// </summary>
        /// <param name="stream">Readable stream positioned at the start of the archive.</param>
        /// <param name="format">Archive format of the stream.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        [ExcludeFromCodeCoverage(
            Justification = "Loads the native 7-Zip library and creates a native COM archive object; exercised end-to-end by the integration test matrix."
        )]
        public SevenZipExtractor(Stream stream, ArchiveFormat format, ILogger<SevenZipExtractor> logger)
        {
            NativeLibraryLoader.Register();
            _format = format;
            _logger = logger;
            _streamAdapter = new InStreamAdapter(stream);
            _archive = SevenZipLib.CreateArchiveObject<IInArchive>(ArchiveFormatRegistry.GetClassId(format));
        }

        // For unit testing — bypasses native library creation.
        internal SevenZipExtractor(Stream stream, ArchiveFormat format, IInArchive archive, ILogger<SevenZipExtractor> logger)
        {
            _format = format;
            _logger = logger;
            _streamAdapter = new InStreamAdapter(stream);
            _archive = archive;
        }

        /// <summary>
        /// Creates a new extractor, returning <c>Result.Fail</c> if the native library cannot be loaded.
        /// </summary>
        /// <param name="stream">Readable stream positioned at the start of the archive.</param>
        /// <param name="format">Archive format of the stream.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <returns>A successful result containing the extractor, or a failed result with the error message.</returns>
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
            Justification = "Ownership of the extractor transfers to the caller via Result<T>; disposing here would return a dead object."
        )]
        public static Result<SevenZipExtractor> Create(Stream stream, ArchiveFormat format, ILogger<SevenZipExtractor> logger)
        {
            try
            {
                return Result.Ok(new SevenZipExtractor(stream, format, logger));
            }
            catch (Exception ex)
            {
                return Result.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Opens the archive and reads its top-level metadata.
        /// </summary>
        /// <param name="password">Password for encrypted archives; <see langword="null"/> for unprotected archives.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A successful result containing archive metadata, or a failed result if the archive could not be opened.</returns>
        /// <remarks>Must be called before <see cref="ListEntriesAsync"/>, <see cref="ExtractAllAsync"/>, <see cref="ExtractEntryAsync"/>, or any <c>ExtractAsync</c> overload.</remarks>
        /// <example>
        /// <code>
        /// using var stream = File.OpenRead("archive.7z");
        /// using var extractor = new SevenZipExtractor(stream, ArchiveFormat.SevenZip, logger);
        /// var info = await extractor.OpenAsync(password: "secret");
        /// if (info.IsFailed) return info.ToResult();
        /// Console.WriteLine($"Solid: {info.Value.IsSolid}, encrypted: {info.Value.IsEncrypted}");
        /// </code>
        /// </example>
        /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
        public async Task<Result<ArchiveInfo>> OpenAsync(string? password = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            return await Task.Run(
                    () =>
                    {
                        _password = password;
                        var handler = new ArchiveOpenHandler(password);
                        var hr = _archive.Open(_streamAdapter, IntPtr.Zero, handler);
                        if (hr != HResult.Ok)
                        {
                            ArchiveOpenFailed(_logger, _format, hr);
                            return Result.Fail<ArchiveInfo>($"Failed to open archive (HRESULT: 0x{hr:X8}).");
                        }

                        var info = ReadArchiveInfo();
                        _opened = true;
                        ArchiveOpened(_logger, _format);
                        return Result.Ok(info);
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Returns metadata for every entry in the archive.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A successful result containing the entry list, or a failed result if listing fails.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
        public async Task<Result<IReadOnlyList<ArchiveEntry>>> ListEntriesAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_opened)
                return Result.Fail<IReadOnlyList<ArchiveEntry>>(NotOpenedMessage);
            return await Task.Run(
                    () =>
                    {
                        var hr = _archive.GetNumberOfItems(out var count);
                        if (hr != HResult.Ok)
                        {
                            ListEntriesFailed(_logger, _format, hr);
                            return Result.Fail<IReadOnlyList<ArchiveEntry>>($"Failed to list archive entries (HRESULT: 0x{hr:X8}).");
                        }

                        if (count > (uint)Array.MaxLength)
                        {
                            return Result.Fail<IReadOnlyList<ArchiveEntry>>($"Archive reports {count} entries, which exceeds the supported maximum.");
                        }

                        var entries = new List<ArchiveEntry>((int)count);
                        for (uint i = 0; i < count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            entries.Add(ReadEntry(i));
                        }

                        return Result.Ok<IReadOnlyList<ArchiveEntry>>(entries);
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Extracts all entries in the archive to the specified output directory.
        /// </summary>
        /// <param name="outputPath">Directory to write extracted files into; created if it does not exist.</param>
        /// <param name="progress">Optional progress sink; receives a snapshot after each block of bytes is processed.</param>
        /// <param name="options">Controls how entries are written to disk; <see langword="null"/> uses defaults (periodic flush-to-disk enabled).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A successful result on completion, or a failed result if extraction fails or any entry has errors.</returns>
        /// <remarks>Entries whose paths resolve outside <paramref name="outputPath"/> (zip-slip) are silently skipped.</remarks>
        /// <example>
        /// <code>
        /// await extractor.OpenAsync();
        /// var progress = new Progress&lt;ExtractionProgress&gt;(p =>
        ///     Console.WriteLine($"{p.EntryPath} — {p.EntryIndex + 1}/{p.TotalEntries}"));
        /// var result = await extractor.ExtractAllAsync("/path/to/output", progress);
        /// </code>
        /// </example>
        /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
        public async Task<Result> ExtractAllAsync(
            string outputPath,
            IProgress<ExtractionProgress>? progress = null,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_opened)
                return Result.Fail(NotOpenedMessage);
            var flushIntervalBytes = options?.FlushIntervalBytes ?? ExtractionOptions.DefaultFlushIntervalBytes;
            return await Task.Run(
                    () =>
                    {
                        var hr = _archive.GetNumberOfItems(out var count);
                        if (hr != HResult.Ok)
                            return Result.Fail($"Failed to get item count (HRESULT: 0x{hr:X8}).");

                        var handler = new ExtractionHandler(
                            CreateFileEntryProvider(outputPath, flushIntervalBytes),
                            progress,
                            (int)count,
                            cancellationToken,
                            _password
                        );

                        hr = _archive.Extract(null, uint.MaxValue, 0, handler);
                        cancellationToken.ThrowIfCancellationRequested();

                        if (hr == HResult.Ok && handler.LastEntryError != OperationResult.Ok)
                        {
                            ExtractionHadEntryErrors(_logger, _format, handler.LastEntryError);
                            return Result.Fail($"Extraction had entry errors: {handler.LastEntryError}.");
                        }

                        if (hr == HResult.Ok)
                        {
                            ExtractAllCompleted(_logger, _format, count);
                            return Result.Ok();
                        }

                        ExtractAllFailed(_logger, _format, hr);
                        return Result.Fail($"Extraction failed (HRESULT: 0x{hr:X8}).");
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Extracts a single archive entry to the provided stream.
        /// </summary>
        /// <param name="entry">The entry to extract; must belong to this archive.</param>
        /// <param name="outputStream">Writable stream that receives the decompressed data.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A successful result on completion, or a failed result if extraction fails or the entry has errors.</returns>
        /// <example>
        /// <code>
        /// await extractor.OpenAsync();
        /// var entries = (await extractor.ListEntriesAsync()).Value;
        /// var readme = entries.First(e => e.Path == "readme.md");
        /// using var output = new MemoryStream();
        /// await extractor.ExtractEntryAsync(readme, output);
        /// </code>
        /// </example>
        /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
        public async Task<Result> ExtractEntryAsync(ArchiveEntry entry, Stream outputStream, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_opened)
                return Result.Fail(NotOpenedMessage);
            return await Task.Run(
                    () =>
                    {
                        var indices = new uint[] { (uint)entry.Index };
                        var handler = new ExtractionHandler(CreateSingleEntryProvider(entry, outputStream), null, 1, cancellationToken, _password);

                        var hr = _archive.Extract(indices, 1, 0, handler);
                        cancellationToken.ThrowIfCancellationRequested();

                        if (hr == HResult.Ok && handler.LastEntryError != OperationResult.Ok)
                        {
                            ExtractionHadEntryErrors(_logger, _format, handler.LastEntryError);
                            return Result.Fail($"Entry extraction had errors: {handler.LastEntryError}.");
                        }

                        if (hr == HResult.Ok)
                        {
                            ExtractEntryCompleted(_logger, entry.Path);
                            return Result.Ok();
                        }

                        ExtractEntryFailed(_logger, entry.Path, hr);
                        return Result.Fail($"Entry extraction failed (HRESULT: 0x{hr:X8}).");
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Extracts entries that match <paramref name="filter"/> to the specified output directory.
        /// </summary>
        /// <param name="filter">Predicate applied to each entry; only matching entries are extracted.</param>
        /// <param name="outputPath">Directory to write extracted files into; created if it does not exist.</param>
        /// <param name="progress">Optional progress sink; receives a snapshot after each block of bytes is processed.</param>
        /// <param name="options">Controls how entries are written to disk; <see langword="null"/> uses defaults (periodic flush-to-disk enabled).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A successful result on completion, or a failed result if extraction fails or any matched entry has errors.</returns>
        /// <remarks>Calls <see cref="ListEntriesAsync"/> internally. Use the <see cref="ExtractAsync(IReadOnlyList{ArchiveEntry},Func{ArchiveEntry,bool},string,IProgress{ExtractionProgress}?,ExtractionOptions?,CancellationToken)"/> overload to avoid the extra round-trip when you already have the entry list.</remarks>
        /// <example>
        /// <code>
        /// await extractor.OpenAsync();
        /// await extractor.ExtractAsync(
        ///     e => e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase),
        ///     "/path/to/output");
        /// </code>
        /// </example>
        /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
        public async Task<Result> ExtractAsync(
            Func<ArchiveEntry, bool> filter,
            string outputPath,
            IProgress<ExtractionProgress>? progress = null,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_opened)
                return Result.Fail(NotOpenedMessage);
            var entriesResult = await ListEntriesAsync(cancellationToken).ConfigureAwait(false);
            if (entriesResult.IsFailed)
                return entriesResult.ToResult();
            return await ExtractAsync(entriesResult.Value, filter, outputPath, progress, options, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Extracts entries that match <paramref name="filter"/> from a pre-built entry list to the specified output directory.
        /// </summary>
        /// <param name="entries">Entry list previously returned by <see cref="ListEntriesAsync"/>.</param>
        /// <param name="filter">Predicate applied to each entry; only matching entries are extracted.</param>
        /// <param name="outputPath">Directory to write extracted files into; created if it does not exist.</param>
        /// <param name="progress">Optional progress sink; receives a snapshot after each block of bytes is processed.</param>
        /// <param name="options">Controls how entries are written to disk; <see langword="null"/> uses defaults (periodic flush-to-disk enabled).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A successful result on completion, or a failed result if extraction fails or any matched entry has errors.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
        public async Task<Result> ExtractAsync(
            IReadOnlyList<ArchiveEntry> entries,
            Func<ArchiveEntry, bool> filter,
            string outputPath,
            IProgress<ExtractionProgress>? progress = null,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_opened)
                return Result.Fail(NotOpenedMessage);
            var flushIntervalBytes = options?.FlushIntervalBytes ?? ExtractionOptions.DefaultFlushIntervalBytes;
            return await Task.Run(
                    () =>
                    {
                        var indices = entries.Where(filter).Select(e => (uint)e.Index).ToArray();

                        if (indices.Length == 0)
                            return Result.Ok();

                        var handler = new ExtractionHandler(
                            CreateFileEntryProvider(outputPath, flushIntervalBytes),
                            progress,
                            indices.Length,
                            cancellationToken,
                            _password
                        );

                        var hr = _archive.Extract(indices, (uint)indices.Length, 0, handler);
                        cancellationToken.ThrowIfCancellationRequested();

                        if (hr == HResult.Ok && handler.LastEntryError != OperationResult.Ok)
                        {
                            ExtractionHadEntryErrors(_logger, _format, handler.LastEntryError);
                            return Result.Fail($"Filtered extraction had entry errors: {handler.LastEntryError}.");
                        }

                        if (hr == HResult.Ok)
                            return Result.Ok();

                        ExtractFilteredFailed(_logger, _format, hr);
                        return Result.Fail($"Filtered extraction failed (HRESULT: 0x{hr:X8}).");
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Releases the underlying native archive object.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            var closeHr = _archive.Close();
            if (closeHr != HResult.Ok)
                ArchiveCloseFailed(_logger, _format, closeHr);
        }

        private ArchiveInfo ReadArchiveInfo() =>
            new ArchiveInfo
            {
                Format = _format,
                IsSolid = ReadBoolArchiveProp(ItemPropId.Solid) ?? false,
                IsEncrypted = ReadBoolArchiveProp(ItemPropId.Encrypted) ?? false,
                Comment = ReadStringArchiveProp(ItemPropId.Comment),
                PhysicalSize = ReadUInt64ArchiveProp(ItemPropId.PhysicalSize) ?? 0,
                VolumeCount = (int)(ReadUInt32ArchiveProp(ItemPropId.NumVolumes) ?? 1),
            };

        private ArchiveEntry ReadEntry(uint index) =>
            new ArchiveEntry
            {
                Index = (int)index,
                // ArchiveEntry.Path is documented as forward-slash separators, matching
                // ZipArchiveEntry.FullName and the ZIP/7z spec convention. 7-Zip on Windows
                // returns backslashes; normalize at the boundary so the contract holds on every OS.
                Path = (ReadStringProp(index, ItemPropId.Path) ?? string.Empty).Replace('\\', '/'),
                Size = ReadUInt64Prop(index, ItemPropId.Size) ?? 0,
                PackedSize = ReadUInt64Prop(index, ItemPropId.PackedSize) ?? 0,
                Crc = ReadUInt32Prop(index, ItemPropId.Crc) ?? 0,
                IsDirectory = ReadBoolProp(index, ItemPropId.IsDirectory) ?? false,
                IsEncrypted = ReadBoolProp(index, ItemPropId.Encrypted) ?? false,
                LastWriteTime = ReadDateTimeProp(index, ItemPropId.LastWriteTime),
                CreationTime = ReadDateTimeProp(index, ItemPropId.CreationTime),
                LastAccessTime = ReadDateTimeProp(index, ItemPropId.LastAccessTime),
                Attributes = ReadUInt32Prop(index, ItemPropId.Attributes),
            };

        // Calls native GetProperty into a 24-byte stack buffer (worst-case PROPVARIANT size:
        // Windows propidlbase.h is 24 on x64; POSIX 7-Zip MyWindows.h is 16 on x64). The first
        // 16 bytes are copied into a PropVariant, which is all our managed struct holds.
        [ExcludeFromCodeCoverage(
            Justification = "Unsafe stackalloc + Marshal pointer bridge; exercised by every property read in the integration test matrix."
        )]
        [SuppressMessage(
            "Security",
            "S6640:Make sure that using \"unsafe\" is safe here.",
            Justification = "Required to obtain a pointer to a stack-allocated 24-byte PROPVARIANT buffer so we can pass it to IInArchive.GetProperty. The buffer is local, the pointer never escapes the fixed block, and the bytes copied back are bounded by buf[..16]. See [[project-interop-gotchas]] round 3 for the size rationale."
        )]
        private unsafe int GetPropertyNative(uint index, ItemPropId propId, out PropVariant prop)
        {
            Span<byte> buf = stackalloc byte[24];
            buf.Clear();
            int hr;
            fixed (byte* p = buf)
                hr = _archive.GetProperty(index, propId, (nint)p);
            prop = new PropVariant();
            if (hr == HResult.Ok)
            {
                var dest = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref prop, 1));
                buf[..16].CopyTo(dest);
            }
            return hr;
        }

        [ExcludeFromCodeCoverage(
            Justification = "Unsafe stackalloc + Marshal pointer bridge; exercised by every archive-level property read in the integration test matrix."
        )]
        [SuppressMessage(
            "Security",
            "S6640:Make sure that using \"unsafe\" is safe here.",
            Justification = "Required to obtain a pointer to a stack-allocated 24-byte PROPVARIANT buffer so we can pass it to IInArchive.GetArchiveProperty. The buffer is local, the pointer never escapes the fixed block, and the bytes copied back are bounded by buf[..16]. See [[project-interop-gotchas]] round 3 for the size rationale."
        )]
        private unsafe int GetArchivePropertyNative(ItemPropId propId, out PropVariant prop)
        {
            Span<byte> buf = stackalloc byte[24];
            buf.Clear();
            int hr;
            fixed (byte* p = buf)
                hr = _archive.GetArchiveProperty(propId, (nint)p);
            prop = new PropVariant();
            if (hr == HResult.Ok)
            {
                var dest = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref prop, 1));
                buf[..16].CopyTo(dest);
            }
            return hr;
        }

        private bool? ReadBoolProp(uint index, ItemPropId propId)
        {
            var hr = GetPropertyNative(index, propId, out var prop);
            var value = hr == HResult.Ok ? prop.ToBoolean() : null;
            prop.Clear();
            return value;
        }

        private string? ReadStringProp(uint index, ItemPropId propId)
        {
            var hr = GetPropertyNative(index, propId, out var prop);
            var value = hr == HResult.Ok ? prop.ToStringValue() : null;
            prop.Clear();
            return value;
        }

        private ulong? ReadUInt64Prop(uint index, ItemPropId propId)
        {
            var hr = GetPropertyNative(index, propId, out var prop);
            var value = hr == HResult.Ok ? prop.ToUInt64() : null;
            prop.Clear();
            return value;
        }

        private uint? ReadUInt32Prop(uint index, ItemPropId propId)
        {
            var hr = GetPropertyNative(index, propId, out var prop);
            var value = hr == HResult.Ok ? prop.ToUInt32() : null;
            prop.Clear();
            return value;
        }

        private DateTime? ReadDateTimeProp(uint index, ItemPropId propId)
        {
            var hr = GetPropertyNative(index, propId, out var prop);
            var value = hr == HResult.Ok ? prop.ToDateTime() : null;
            prop.Clear();
            return value;
        }

        private bool? ReadBoolArchiveProp(ItemPropId propId)
        {
            var hr = GetArchivePropertyNative(propId, out var prop);
            var value = hr == HResult.Ok ? prop.ToBoolean() : null;
            prop.Clear();
            return value;
        }

        private string? ReadStringArchiveProp(ItemPropId propId)
        {
            var hr = GetArchivePropertyNative(propId, out var prop);
            var value = hr == HResult.Ok ? prop.ToStringValue() : null;
            prop.Clear();
            return value;
        }

        private ulong? ReadUInt64ArchiveProp(ItemPropId propId)
        {
            var hr = GetArchivePropertyNative(propId, out var prop);
            var value = hr == HResult.Ok ? prop.ToUInt64() : null;
            prop.Clear();
            return value;
        }

        private uint? ReadUInt32ArchiveProp(ItemPropId propId)
        {
            var hr = GetArchivePropertyNative(propId, out var prop);
            var value = hr == HResult.Ok ? prop.ToUInt32() : null;
            prop.Clear();
            return value;
        }

        private Func<uint, (ISequentialOutStream? Stream, string EntryPath)> CreateFileEntryProvider(string outputPath, long flushIntervalBytes)
        {
            var canonicalOutput =
                Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            // One pacer shared by every entry in this extraction so the flush cadence bounds the
            // aggregate write backlog, not just the backlog within a single large file. No locking
            // needed: 7-Zip opens, writes, and closes entry streams one at a time (ExtractionHandler).
            var flushPacer = new WriteFlushPacer(flushIntervalBytes);

            return index =>
            {
                var path = ReadStringProp(index, ItemPropId.Path) ?? string.Empty;
                var isDir = ReadBoolProp(index, ItemPropId.IsDirectory) ?? false;

                if (string.IsNullOrEmpty(path))
                    return (null, path);

                var fullPath = Path.GetFullPath(Path.Combine(outputPath, path));

                // Guard against path traversal (zip slip): skip entries that resolve outside the output directory.
                if (!fullPath.StartsWith(canonicalOutput, StringComparison.Ordinal))
                    return (null, path);

                if (isDir)
                {
                    Directory.CreateDirectory(fullPath);
                    return (null, path);
                }

                return (new FileEntryStream(fullPath, flushPacer), path);
            };
        }

        private static Func<uint, (ISequentialOutStream? Stream, string EntryPath)> CreateSingleEntryProvider(ArchiveEntry entry, Stream outputStream)
        {
            var adapter = new OutStreamAdapter(outputStream);
            return index => (index == (uint)entry.Index ? (ISequentialOutStream?)adapter : null, entry.Path);
        }
    }
}
