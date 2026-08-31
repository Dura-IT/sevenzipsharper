using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.UnitTests;

[TestOf(typeof(SevenZipExtractor))]
public sealed class SevenZipExtractorTests
{
    private static SevenZipExtractor CreateExtractor(IInArchive? archive = null) =>
        new SevenZipExtractor(Stream.Null, ArchiveFormat.SevenZip, archive ?? new FakeArchiveForExtraction(), NullLogger<SevenZipExtractor>.Instance);

    private static ArchiveEntry TestEntry(int index = 0, string path = "entry.txt") =>
        new ArchiveEntry
        {
            Index = index,
            Path = path,
            Size = 0,
            PackedSize = 0,
            Crc = 0,
            IsDirectory = false,
            IsEncrypted = false,
        };

    // ── OpenAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task OpenAsync_ReturnsFail_WhenArchiveOpenReturnsError()
    {
        using var extractor = CreateExtractor(new FakeArchiveForExtraction(openHResult: HResult.NotImplemented));

        var result = await extractor.OpenAsync();

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task OpenAsync_ReturnsOk_WhenArchiveOpenSucceeds()
    {
        using var extractor = CreateExtractor();

        var result = await extractor.OpenAsync();

        result.IsSuccess.Should().BeTrue();
    }

    // ── ListEntriesAsync ───────────────────────────────────────────────────

    [Test]
    public async Task ListEntriesAsync_ReturnsEntry_ForEachItemInArchive()
    {
        using var extractor = CreateExtractor(new FakeArchiveForExtraction(entries: new[] { ("file.txt", false) }));
        await extractor.OpenAsync();

        var result = await extractor.ListEntriesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Path.Should().Be("file.txt");
        result.Value[0].IsDirectory.Should().BeFalse();
    }

    [Test]
    public async Task ListEntriesAsync_ReturnsFail_WhenOpenAsyncNotCalled()
    {
        using var extractor = CreateExtractor();

        var result = await extractor.ListEntriesAsync();

        result.IsFailed.Should().BeTrue();
    }

    // ── ExtractAllAsync — path traversal (security) ────────────────────────

    [Test]
    public async Task ExtractAllAsync_SkipsEntry_WhenPathTraversesOutsideOutputDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var content = new byte[] { 1, 2, 3 };
            var archive = new FakeArchiveForExtraction(
                entries: new[] { ("../escaped.txt", false) },
                extractCallback: (callback, i) =>
                {
                    callback.GetStream(i, out var stream, AskMode.Extract);
                    stream?.Write(content, (uint)content.Length, out _);
                    callback.PrepareOperation(AskMode.Extract);
                    callback.SetOperationResult(OperationResult.Ok);
                }
            );
            using var extractor = CreateExtractor(archive);
            await extractor.OpenAsync();

            await extractor.ExtractAllAsync(tempDir);

            var escapedPath = Path.GetFullPath(Path.Combine(tempDir, "../escaped.txt"));
            File.Exists(escapedPath).Should().BeFalse("path traversal entries must be skipped");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task ExtractAllAsync_ExtractsEntry_WhenPathIsInsideOutputDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var content = new byte[] { 7, 8, 9 };
            var archive = new FakeArchiveForExtraction(
                entries: new[] { ("safe.txt", false) },
                extractCallback: (callback, i) =>
                {
                    callback.GetStream(i, out var stream, AskMode.Extract);
                    stream?.Write(content, (uint)content.Length, out _);
                    callback.PrepareOperation(AskMode.Extract);
                    callback.SetOperationResult(OperationResult.Ok);
                }
            );
            using var extractor = CreateExtractor(archive);
            await extractor.OpenAsync();

            var result = await extractor.ExtractAllAsync(tempDir);

            result.IsSuccess.Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "safe.txt")).Should().BeTrue();
            var bytes = await File.ReadAllBytesAsync(Path.Combine(tempDir, "safe.txt"));
            bytes.Should().Equal(content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── ExtractAllAsync — entry errors ─────────────────────────────────────

    [Test]
    public async Task ExtractAllAsync_ReturnsFail_WhenWorstEntryResultIsCrcError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var archive = new FakeArchiveForExtraction(
                entries: new[] { ("corrupt.bin", false) },
                extractCallback: (callback, i) =>
                {
                    callback.GetStream(i, out _, AskMode.Extract);
                    callback.PrepareOperation(AskMode.Extract);
                    callback.SetOperationResult(OperationResult.CrcError);
                }
            );
            using var extractor = CreateExtractor(archive);
            await extractor.OpenAsync();

            var result = await extractor.ExtractAllAsync(tempDir);

            result.IsFailed.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task ExtractAllAsync_ReturnsFail_WhenOpenAsyncNotCalled()
    {
        using var extractor = CreateExtractor();

        var result = await extractor.ExtractAllAsync(Path.GetTempPath());

        result.IsFailed.Should().BeTrue();
    }

    // ── ExtractEntryAsync ──────────────────────────────────────────────────

    [Test]
    public async Task ExtractEntryAsync_WritesEntryData_ToOutputStream()
    {
        var content = new byte[] { 7, 8, 9 };
        var archive = new FakeArchiveForExtraction(
            entries: new[] { ("entry.txt", false) },
            extractCallback: (callback, i) =>
            {
                callback.GetStream(i, out var stream, AskMode.Extract);
                stream?.Write(content, (uint)content.Length, out _);
                callback.PrepareOperation(AskMode.Extract);
                callback.SetOperationResult(OperationResult.Ok);
            }
        );
        using var extractor = CreateExtractor(archive);
        await extractor.OpenAsync();
        var output = new MemoryStream();

        var result = await extractor.ExtractEntryAsync(TestEntry(), output);

        result.IsSuccess.Should().BeTrue();
        output.ToArray().Should().Equal(content);
    }

    [Test]
    public async Task ExtractEntryAsync_ReturnsFail_WhenExtractReturnsError()
    {
        var archive = new FakeArchiveForExtraction(entries: new[] { ("entry.txt", false) }, extractHResult: HResult.NotImplemented);
        using var extractor = CreateExtractor(archive);
        await extractor.OpenAsync();

        var result = await extractor.ExtractEntryAsync(TestEntry(), new MemoryStream());

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task ExtractEntryAsync_ReturnsFail_WhenEntryResultIsCrcError()
    {
        var archive = new FakeArchiveForExtraction(
            entries: new[] { ("entry.txt", false) },
            extractCallback: (callback, i) =>
            {
                callback.GetStream(i, out _, AskMode.Extract);
                callback.PrepareOperation(AskMode.Extract);
                callback.SetOperationResult(OperationResult.CrcError);
            }
        );
        using var extractor = CreateExtractor(archive);
        await extractor.OpenAsync();

        var result = await extractor.ExtractEntryAsync(TestEntry(), new MemoryStream());

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task ExtractEntryAsync_PropagatesCancellation()
    {
        using var extractor = CreateExtractor();
        await extractor.OpenAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        await FluentActions
            .Awaiting(() => extractor.ExtractEntryAsync(TestEntry(), new MemoryStream(), cts.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    // ── ExtractAsync(filter) ───────────────────────────────────────────────

    [Test]
    public async Task ExtractAsync_ExtractsOnlyMatchingEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var content = new byte[] { 1, 2, 3 };
            var archive = new FakeArchiveForExtraction(
                entries: new[] { ("match.txt", false), ("skip.txt", false) },
                extractCallback: (callback, i) =>
                {
                    callback.GetStream(i, out var stream, AskMode.Extract);
                    stream?.Write(content, (uint)content.Length, out _);
                    callback.PrepareOperation(AskMode.Extract);
                    callback.SetOperationResult(OperationResult.Ok);
                }
            );
            using var extractor = CreateExtractor(archive);
            await extractor.OpenAsync();

            var result = await extractor.ExtractAsync(e => e.Path == "match.txt", tempDir);

            result.IsSuccess.Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "match.txt")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "skip.txt")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task ExtractAsync_ReturnsOk_WhenNoEntriesMatchFilter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var archive = new FakeArchiveForExtraction(entries: new[] { ("file.txt", false) });
            using var extractor = CreateExtractor(archive);
            await extractor.OpenAsync();

            var result = await extractor.ExtractAsync(_ => false, tempDir);

            result.IsSuccess.Should().BeTrue();
            Directory.GetFiles(tempDir).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task ExtractAsync_ReturnsFail_WhenEntryExtractionFails()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var archive = new FakeArchiveForExtraction(
                entries: new[] { ("file.txt", false) },
                extractCallback: (callback, i) =>
                {
                    callback.GetStream(i, out _, AskMode.Extract);
                    callback.PrepareOperation(AskMode.Extract);
                    callback.SetOperationResult(OperationResult.CrcError);
                }
            );
            using var extractor = CreateExtractor(archive);
            await extractor.OpenAsync();

            var result = await extractor.ExtractAsync(_ => true, tempDir);

            result.IsFailed.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── ExtractAsync(entries, filter) ─────────────────────────────────────

    [Test]
    public async Task ExtractAsync_WithPrebuiltEntries_ExtractsMatchingEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var content = new byte[] { 5, 6, 7 };
            var archive = new FakeArchiveForExtraction(
                entries: new[] { ("a.txt", false), ("b.txt", false) },
                extractCallback: (callback, i) =>
                {
                    callback.GetStream(i, out var stream, AskMode.Extract);
                    stream?.Write(content, (uint)content.Length, out _);
                    callback.PrepareOperation(AskMode.Extract);
                    callback.SetOperationResult(OperationResult.Ok);
                }
            );
            using var extractor = CreateExtractor(archive);
            await extractor.OpenAsync();
            var prebuiltEntries = new List<ArchiveEntry> { TestEntry(0, "a.txt"), TestEntry(1, "b.txt") };

            var result = await extractor.ExtractAsync(prebuiltEntries, e => e.Path == "a.txt", tempDir);

            result.IsSuccess.Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "a.txt")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "b.txt")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── ObjectDisposedException guards ────────────────────────────────────

    [Test]
    public async Task OpenAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        var extractor = CreateExtractor();
        extractor.Dispose();

        await FluentActions.Awaiting(() => extractor.OpenAsync()).Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task ListEntriesAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        var extractor = CreateExtractor();
        extractor.Dispose();

        await FluentActions.Awaiting(() => extractor.ListEntriesAsync()).Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task ExtractAllAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        var extractor = CreateExtractor();
        extractor.Dispose();

        await FluentActions.Awaiting(() => extractor.ExtractAllAsync(Path.GetTempPath())).Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task ExtractEntryAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        var extractor = CreateExtractor();
        extractor.Dispose();

        await FluentActions.Awaiting(() => extractor.ExtractEntryAsync(TestEntry(), new MemoryStream())).Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task ExtractAsync_WithFilter_WhenDisposed_ThrowsObjectDisposedException()
    {
        var extractor = CreateExtractor();
        extractor.Dispose();

        await FluentActions.Awaiting(() => extractor.ExtractAsync(_ => true, Path.GetTempPath())).Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task ExtractAsync_WithPrebuiltEntries_WhenDisposed_ThrowsObjectDisposedException()
    {
        var extractor = CreateExtractor();
        extractor.Dispose();

        await FluentActions.Awaiting(() => extractor.ExtractAsync([], _ => true, Path.GetTempPath())).Should().ThrowAsync<ObjectDisposedException>();
    }

    // ── Not-opened guards ─────────────────────────────────────────────────

    [Test]
    public async Task ExtractAsync_WithFilter_ReturnsFail_WhenOpenAsyncNotCalled()
    {
        using var extractor = CreateExtractor();

        var result = await extractor.ExtractAsync(_ => true, Path.GetTempPath());

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task ExtractAsync_WithPrebuiltEntries_ReturnsFail_WhenOpenAsyncNotCalled()
    {
        using var extractor = CreateExtractor();

        var result = await extractor.ExtractAsync([], _ => true, Path.GetTempPath());

        result.IsFailed.Should().BeTrue();
    }

    // ── GetNumberOfItems failure ───────────────────────────────────────────

    [Test]
    public async Task ListEntriesAsync_ReturnsFail_WhenGetNumberOfItemsFails()
    {
        using var extractor = CreateExtractor(new FakeArchiveForExtraction(getNumberOfItemsHResult: HResult.NotImplemented));
        await extractor.OpenAsync();

        var result = await extractor.ListEntriesAsync();

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task ExtractAllAsync_ReturnsFail_WhenGetNumberOfItemsFails()
    {
        using var extractor = CreateExtractor(new FakeArchiveForExtraction(getNumberOfItemsHResult: HResult.NotImplemented));
        await extractor.OpenAsync();

        var result = await extractor.ExtractAllAsync(Path.GetTempPath());

        result.IsFailed.Should().BeTrue();
    }

    // ── Extract HRESULT failure ───────────────────────────────────────────

    [Test]
    public async Task ExtractAllAsync_ReturnsFail_WhenExtractReturnsError()
    {
        using var extractor = CreateExtractor(new FakeArchiveForExtraction(entries: new[] { ("file.txt", false) }, extractHResult: HResult.NotImplemented));
        await extractor.OpenAsync();

        var result = await extractor.ExtractAllAsync(Path.GetTempPath());

        result.IsFailed.Should().BeTrue();
    }

    // ── Cancellation propagation ──────────────────────────────────────────

    [Test]
    public async Task ExtractAllAsync_PropagatesCancellation()
    {
        using var extractor = CreateExtractor();
        await extractor.OpenAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        await FluentActions
            .Awaiting(() => extractor.ExtractAllAsync(Path.GetTempPath(), cancellationToken: cts.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task ExtractAsync_WithFilter_PropagatesCancellation()
    {
        using var extractor = CreateExtractor();
        await extractor.OpenAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        await FluentActions
            .Awaiting(() => extractor.ExtractAsync(_ => true, Path.GetTempPath(), cancellationToken: cts.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task ExtractAsync_WithPrebuiltEntries_PropagatesCancellation()
    {
        using var extractor = CreateExtractor();
        await extractor.OpenAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        await FluentActions
            .Awaiting(() => extractor.ExtractAsync([], _ => true, Path.GetTempPath(), cancellationToken: cts.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    // ── ExtractAsync(prebuilt entries) failure ────────────────────────────

    [Test]
    public async Task ExtractAsync_WithPrebuiltEntries_ReturnsFail_WhenExtractFails()
    {
        var archive = new FakeArchiveForExtraction(entries: new[] { ("file.txt", false) }, extractHResult: HResult.NotImplemented);
        using var extractor = CreateExtractor(archive);
        await extractor.OpenAsync();
        var entries = new List<ArchiveEntry> { TestEntry(0, "file.txt") };

        var result = await extractor.ExtractAsync(entries, _ => true, Path.GetTempPath());

        result.IsFailed.Should().BeTrue();
    }

    // ── Dispose ────────────────────────────────────────────────────────────

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var extractor = CreateExtractor();
        extractor.Dispose();

        var act = () => extractor.Dispose();

        act.Should().NotThrow();
    }
}

[GeneratedComClass]
internal sealed partial class FakeArchiveForExtraction : IInArchive
{
    private readonly int _openHResult;
    private readonly int _extractHResult;
    private readonly (string Path, bool IsDirectory)[] _entries;
    private readonly Action<IArchiveExtractCallback, uint>? _extractCallback;

    private readonly int _getNumberOfItemsHResult;

    internal FakeArchiveForExtraction(
        int openHResult = HResult.Ok,
        (string Path, bool IsDirectory)[]? entries = null,
        Action<IArchiveExtractCallback, uint>? extractCallback = null,
        int extractHResult = HResult.Ok,
        int getNumberOfItemsHResult = HResult.Ok
    )
    {
        _openHResult = openHResult;
        _extractHResult = extractHResult;
        _getNumberOfItemsHResult = getNumberOfItemsHResult;
        _entries = entries ?? Array.Empty<(string, bool)>();
        _extractCallback = extractCallback;
    }

    public int Open(IInStream stream, IntPtr maxCheckStartPosition, IArchiveOpenCallback? openArchiveCallback) => _openHResult;

    public int Close() => HResult.Ok;

    public int GetNumberOfItems(out uint numItems)
    {
        numItems = _getNumberOfItemsHResult == HResult.Ok ? (uint)_entries.Length : 0;
        return _getNumberOfItemsHResult;
    }

    public int GetProperty(uint index, ItemPropId propId, nint value)
    {
        if (index >= (uint)_entries.Length)
            return HResult.InvalidArg;

        var prop = propId switch
        {
            ItemPropId.Path => PropVariant.FromString(_entries[index].Path),
            ItemPropId.IsDirectory => PropVariant.FromBoolean(_entries[index].IsDirectory),
            _ => new PropVariant(),
        };
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref prop, 1));
        for (var i = 0; i < bytes.Length; i++)
            System.Runtime.InteropServices.Marshal.WriteByte(value, i, bytes[i]);
        return HResult.Ok;
    }

    public int Extract(uint[]? indices, uint numItems, int testMode, IArchiveExtractCallback extractCallback)
    {
        var count = indices != null ? (uint)indices.Length : (uint)_entries.Length;
        for (uint i = 0; i < count; i++)
        {
            var idx = indices != null ? indices[i] : i;
            if (_extractCallback != null)
            {
                _extractCallback(extractCallback, idx);
            }
            else
            {
                extractCallback.GetStream(idx, out _, AskMode.Extract);
                extractCallback.PrepareOperation(AskMode.Extract);
                extractCallback.SetOperationResult(OperationResult.Ok);
            }
        }
        return _extractHResult;
    }

    public int GetArchiveProperty(ItemPropId propId, nint value) => HResult.Ok;

    public int GetNumberOfProperties(out uint numProps)
    {
        numProps = 0;
        return HResult.Ok;
    }

    public int GetPropertyInfo(uint index, out string? name, out uint propId, out ushort varType)
    {
        name = null;
        propId = 0;
        varType = 0;
        return HResult.Ok;
    }

    public int GetNumberOfArchiveProperties(out uint numProps) => GetNumberOfProperties(out numProps);

    public int GetArchivePropertyInfo(uint index, out string? name, out uint propId, out ushort varType) =>
        GetPropertyInfo(index, out name, out propId, out varType);
}
