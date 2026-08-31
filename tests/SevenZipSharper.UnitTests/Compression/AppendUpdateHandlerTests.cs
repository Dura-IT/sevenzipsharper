using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Compression;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.UnitTests.Compression;

[TestOf(typeof(AppendUpdateHandler))]
public sealed class AppendUpdateHandlerTests
{
    private static AppendUpdateHandler CreateHandler(
        FakeInArchiveForAppend? existingArchive = null,
        uint existingCount = 1,
        IReadOnlyList<(string EntryPath, Stream Data)>? newEntries = null,
        IProgress<CompressionProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        existingArchive ??= new FakeInArchiveForAppend(existingCount);
        newEntries ??= new[] { ("new.txt", (Stream)new MemoryStream(new byte[] { 1, 2, 3 })) };
        return new AppendUpdateHandler(
            existingArchive,
            existingCount,
            newEntries,
            progress,
            cancellationToken
        );
    }

    [Test]
    public void GetUpdateItemInfo_ExistingIndex_ReturnsPassThrough()
    {
        var handler = CreateHandler(existingCount: 2);
        IArchiveUpdateCallback cb = handler;
        var pNewData = Marshal.AllocHGlobal(4);
        var pNewProps = Marshal.AllocHGlobal(4);
        var pIdx = Marshal.AllocHGlobal(4);
        try
        {
            var hr = cb.GetUpdateItemInfo(0, pNewData, pNewProps, pIdx);

            hr.Should().Be(HResult.Ok);
            Marshal.ReadInt32(pNewData).Should().Be(0);
            Marshal.ReadInt32(pNewProps).Should().Be(0);
            Marshal.ReadInt32(pIdx).Should().Be(0);
        }
        finally
        {
            Marshal.FreeHGlobal(pNewData);
            Marshal.FreeHGlobal(pNewProps);
            Marshal.FreeHGlobal(pIdx);
        }
    }

    [Test]
    public void GetUpdateItemInfo_NewIndex_ReturnsNewItem()
    {
        var handler = CreateHandler(existingCount: 1);
        IArchiveUpdateCallback cb = handler;
        var pNewData = Marshal.AllocHGlobal(4);
        var pNewProps = Marshal.AllocHGlobal(4);
        var pIdx = Marshal.AllocHGlobal(4);
        try
        {
            var hr = cb.GetUpdateItemInfo(1, pNewData, pNewProps, pIdx);

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
    public void GetProperty_ExistingIndex_DelegatesToInArchive()
    {
        var existing = new FakeInArchiveForAppend(path: "existing.bin");
        var handler = CreateHandler(existingArchive: existing, existingCount: 1);
        IArchiveUpdateCallback cb = handler;
        var prop = new PropVariant();

        cb.GetProperty(0, ItemPropId.Path, ref prop);

        prop.ToStringValue().Should().Be("existing.bin");
        prop.Clear();
    }

    [Test]
    public void GetProperty_NewIndex_ReturnsEntryPath()
    {
        var newEntries = new[] { ("appended.txt", (Stream)new MemoryStream(new byte[] { 9 })) };
        var handler = CreateHandler(existingCount: 1, newEntries: newEntries);
        IArchiveUpdateCallback cb = handler;
        var prop = new PropVariant();

        cb.GetProperty(1, ItemPropId.Path, ref prop);

        prop.ToStringValue().Should().Be("appended.txt");
        prop.Clear();
    }

    [Test]
    public void GetStream_ExistingIndex_ReturnsNull()
    {
        var handler = CreateHandler(existingCount: 2);
        IArchiveUpdateCallback cb = handler;

        var hr = cb.GetStream(0, out var stream);

        hr.Should().Be(HResult.Ok);
        stream.Should().BeNull();
    }

    [Test]
    public void GetStream_NewIndex_ReturnsStream()
    {
        var handler = CreateHandler(existingCount: 1);
        IArchiveUpdateCallback cb = handler;

        var hr = cb.GetStream(1, out var stream);

        hr.Should().Be(HResult.Ok);
        stream.Should().NotBeNull();
    }

    [Test]
    public void CryptoGetTextPassword2_WithPasswordSet_ReturnsPasswordWithIsDefinedOne()
    {
        var handler = new AppendUpdateHandler(
            new FakeInArchiveForAppend(),
            existingCount: 1,
            newEntries: new[] { ("new.txt", (Stream)new MemoryStream(new byte[] { 1 })) },
            progress: null,
            cancellationToken: CancellationToken.None,
            password: "hunter2"
        );
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
    public void SetCompleted_ReturnsAbort_WhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = CreateHandler(cancellationToken: cts.Token);
        IArchiveUpdateCallback cb = handler;

        cb.SetCompleted(nint.Zero).Should().Be(HResult.Abort);
    }

    [Test]
    public void SetCompleted_ReportsProgress_WithNewEntryPath()
    {
        var reported = new List<CompressionProgress>();
        var progress = new SynchronousProgress<CompressionProgress>(p => reported.Add(p));
        var newEntries = new[] { ("appended.txt", (Stream)new MemoryStream(new byte[] { 1 })) };
        var handler = CreateHandler(existingCount: 1, newEntries: newEntries, progress: progress);
        IArchiveUpdateCallback cb = handler;
        cb.SetTotal(500);
        cb.SetOperationResult(OperationResult.Ok);
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
        reported[0].EntryPath.Should().Be("appended.txt");
    }
}

file sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _callback;

    internal SynchronousProgress(Action<T> callback)
    {
        _callback = callback;
    }

    public void Report(T value) => _callback(value);
}

[GeneratedComClass]
internal sealed partial class FakeInArchiveForAppend : IInArchive
{
    private readonly uint _count;
    private readonly string _path;

    internal FakeInArchiveForAppend(uint count = 1, string path = "existing.txt")
    {
        _count = count;
        _path = path;
    }

    public int Open(
        IInStream stream,
        IntPtr maxCheckStartPosition,
        IArchiveOpenCallback? openArchiveCallback
    ) => HResult.Ok;

    public int Close() => HResult.Ok;

    public int GetNumberOfItems(out uint numItems)
    {
        numItems = _count;
        return HResult.Ok;
    }

    public int GetProperty(uint index, ItemPropId propId, nint value)
    {
        var prop = propId switch
        {
            ItemPropId.Path => PropVariant.FromString(_path),
            ItemPropId.IsDirectory => PropVariant.FromBoolean(false),
            _ => new PropVariant(),
        };
        var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref prop, 1));
        for (var i = 0; i < bytes.Length; i++)
            Marshal.WriteByte(value, i, bytes[i]);
        return HResult.Ok;
    }

    public int Extract(
        uint[]? indices,
        uint numItems,
        int testMode,
        IArchiveExtractCallback extractCallback
    ) => HResult.Ok;

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

    public int GetNumberOfArchiveProperties(out uint numProps) =>
        GetNumberOfProperties(out numProps);

    public int GetArchivePropertyInfo(
        uint index,
        out string? name,
        out uint propId,
        out ushort varType
    ) => GetPropertyInfo(index, out name, out propId, out varType);
}
