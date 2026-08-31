using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Compression;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;

namespace SevenZipSharper.UnitTests.Compression;

file sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _callback;

    internal SynchronousProgress(Action<T> callback)
    {
        _callback = callback;
    }

    public void Report(T value) => _callback(value);
}

[TestOf(typeof(CompressionHandler))]
public sealed class CompressionHandlerTests
{
    private static (string EntryPath, Stream Data) MakeEntry(string path, byte[]? content = null)
    {
        var data = new MemoryStream(content ?? new byte[] { 1, 2, 3 });
        return (path, data);
    }

    private static CompressionHandler CreateHandler(
        IReadOnlyList<(string EntryPath, Stream Data)>? entries = null,
        IProgress<CompressionProgress>? progress = null,
        CancellationToken cancellationToken = default
    ) => new CompressionHandler(entries ?? new[] { MakeEntry("file.txt") }, progress, cancellationToken);

    [Test]
    public void SetTotal_WithAnyValue_ReturnsOk()
    {
        var handler = CreateHandler();
        IArchiveUpdateCallback cb = handler;

        cb.SetTotal(1000).Should().Be(HResult.Ok);
    }

    [Test]
    public void SetCompleted_ReturnsOk_WhenNotCancelled()
    {
        var handler = CreateHandler();
        IArchiveUpdateCallback cb = handler;
        cb.SetTotal(1000);

        cb.SetCompleted(nint.Zero).Should().Be(HResult.Ok);
    }

    [Test]
    public void SetCompleted_ReturnsAbort_WhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = CreateHandler(cancellationToken: cts.Token);
        IArchiveUpdateCallback cb = handler;

        cb.SetCompleted(nint.Zero).Should().Be(HResult.Abort);
    }

    [Test]
    public void SetCompleted_WithProgressCallback_ReportsProgress()
    {
        var reported = new List<CompressionProgress>();
        var progress = new SynchronousProgress<CompressionProgress>(p => reported.Add(p));
        var handler = CreateHandler(entries: new[] { MakeEntry("readme.txt") }, progress: progress);
        IArchiveUpdateCallback cb = handler;
        cb.SetTotal(500);
        var ptr = Marshal.AllocHGlobal(sizeof(ulong));
        try
        {
            Marshal.WriteInt64(ptr, (long)100UL);
            cb.SetCompleted(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        reported.Should().HaveCount(1);
        reported[0].EntryPath.Should().Be("readme.txt");
        reported[0].BytesProcessed.Should().Be(100);
        reported[0].TotalBytes.Should().Be(500);
    }

    [Test]
    public void GetUpdateItemInfo_ReturnsNewItemValues()
    {
        var handler = CreateHandler();
        IArchiveUpdateCallback cb = handler;
        var pNewData = Marshal.AllocHGlobal(4);
        var pNewProps = Marshal.AllocHGlobal(4);
        var pIdx = Marshal.AllocHGlobal(4);
        try
        {
            var hr = cb.GetUpdateItemInfo(0, pNewData, pNewProps, pIdx);

            hr.Should().Be(HResult.Ok);
            Marshal.ReadInt32(pNewData).Should().Be(1);
            Marshal.ReadInt32(pNewProps).Should().Be(1);
            unchecked((uint)Marshal.ReadInt32(pIdx)).Should().Be(uint.MaxValue);
        }
        finally
        {
            Marshal.FreeHGlobal(pNewData);
            Marshal.FreeHGlobal(pNewProps);
            Marshal.FreeHGlobal(pIdx);
        }
    }

    [Test]
    public void GetUpdateItemInfo_WithNullOutPointers_ReturnsOk()
    {
        var handler = CreateHandler();
        IArchiveUpdateCallback cb = handler;

        var hr = cb.GetUpdateItemInfo(0, nint.Zero, nint.Zero, nint.Zero);

        hr.Should().Be(HResult.Ok);
    }

    [Test]
    public void GetProperty_Path_ReturnsEntryPath()
    {
        var handler = CreateHandler(entries: new[] { MakeEntry("folder/file.bin") });
        IArchiveUpdateCallback cb = handler;
        var prop = new PropVariant();

        cb.GetProperty(0, ItemPropId.Path, ref prop);

        prop.ToStringValue().Should().Be("folder/file.bin");
        prop.Clear();
    }

    [Test]
    public void GetProperty_Size_ReturnsStreamLength_WhenSeekable()
    {
        var content = new byte[128];
        var handler = CreateHandler(entries: new[] { MakeEntry("a.bin", content) });
        IArchiveUpdateCallback cb = handler;
        var prop = new PropVariant();

        cb.GetProperty(0, ItemPropId.Size, ref prop);

        prop.ToUInt64().Should().Be(128);
    }

    [Test]
    public void GetProperty_Size_ReturnsEmpty_WhenNonSeekable()
    {
        var nonSeekable = new NonSeekableStream();
        var handler = CreateHandler(entries: new[] { ("ns.bin", (Stream)nonSeekable) });
        IArchiveUpdateCallback cb = handler;
        var prop = new PropVariant();

        cb.GetProperty(0, ItemPropId.Size, ref prop);

        prop.ToUInt64().Should().BeNull();
    }

    [Test]
    public void GetProperty_IsDirectory_ReturnsFalse()
    {
        var handler = CreateHandler();
        IArchiveUpdateCallback cb = handler;
        var prop = new PropVariant();

        cb.GetProperty(0, ItemPropId.IsDirectory, ref prop);

        prop.ToBoolean().Should().BeFalse();
    }

    [Test]
    public void GetStream_ReturnsOk_AndSetsStream()
    {
        var handler = CreateHandler();
        IArchiveUpdateCallback cb = handler;

        var hr = cb.GetStream(0, out var stream);

        hr.Should().Be(HResult.Ok);
        stream.Should().NotBeNull();
    }

    [Test]
    public void GetStream_ReturnsInvalidArg_ForOutOfRangeIndex()
    {
        var handler = CreateHandler(entries: new[] { MakeEntry("a.txt") });
        IArchiveUpdateCallback cb = handler;

        var hr = cb.GetStream(1, out _);

        hr.Should().Be(HResult.InvalidArg);
    }

    [Test]
    public void SetOperationResult_ReturnsOk()
    {
        var handler = CreateHandler();
        IArchiveUpdateCallback cb = handler;
        cb.GetStream(0, out _);

        cb.SetOperationResult(OperationResult.Ok).Should().Be(HResult.Ok);
    }

    [Test]
    public void SetOperationResult_DoesNotDisposeCallerStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var handler = CreateHandler(entries: new[] { ("f.bin", (Stream)ms) });
        IArchiveUpdateCallback cb = handler;
        cb.GetStream(0, out _);

        cb.SetOperationResult(OperationResult.Ok);

        FluentActions.Invoking(() => ms.ReadByte()).Should().NotThrow("caller owns the stream");
    }

    [Test]
    public void CryptoGetTextPassword2_WithPasswordSet_ReturnsPasswordWithIsDefinedOne()
    {
        var handler = new CompressionHandler(new[] { MakeEntry("file.txt") }, null, CancellationToken.None, password: "hunter2");
        ICryptoGetTextPassword2 crypto = handler;

        var hr = crypto.CryptoGetTextPassword2(out var isDefined, out var password);

        hr.Should().Be(HResult.Ok);
        isDefined.Should().Be(1);
        password.Should().Be("hunter2");
    }

    [Test]
    public void CryptoGetTextPassword2_WithoutPassword_ReturnsZeroAndEmptyString()
    {
        var handler = CreateHandler();
        ICryptoGetTextPassword2 crypto = handler;

        var hr = crypto.CryptoGetTextPassword2(out var isDefined, out var password);

        hr.Should().Be(HResult.Ok);
        isDefined.Should().Be(0);
        password.Should().Be(string.Empty);
    }

    [Test]
    public void GetStream_DisposesEntryStream_WhenOwnsEntryStreams()
    {
        var first = new TrackingStream(new byte[] { 1, 2, 3 });
        var second = new TrackingStream(new byte[] { 4, 5, 6 });
        var entries = new (string EntryPath, Stream Data)[] { ("a.bin", first), ("b.bin", second) };
        var handler = new CompressionHandler(entries, null, CancellationToken.None, ownsEntryStreams: true);
        IArchiveUpdateCallback cb = handler;

        cb.GetStream(0, out _);
        first.IsDisposed.Should().BeFalse("not yet moved past first entry");

        cb.GetStream(1, out _);
        first.IsDisposed.Should().BeTrue("GetStream(1) should dispose the first entry's stream");
        second.IsDisposed.Should().BeFalse("second entry is still in progress");
    }
}

file sealed class TrackingStream : MemoryStream
{
    internal bool IsDisposed { get; private set; }

    internal TrackingStream(byte[] data)
        : base(data) { }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}

file sealed class NonSeekableStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => 0;

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
