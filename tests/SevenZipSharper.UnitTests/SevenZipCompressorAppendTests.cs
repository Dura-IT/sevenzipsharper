using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.UnitTests.Compression;

namespace SevenZipSharper.UnitTests;

[TestOf(typeof(SevenZipCompressor))]
public sealed class SevenZipCompressorAppendTests
{
    private static SevenZipCompressor CreateCompressor(IOutArchive? archive = null, CompressionParameters? parameters = null) =>
        new SevenZipCompressor(
            ArchiveFormat.SevenZip,
            parameters ?? CompressionParameters.Default,
            archive ?? new FakeOutArchive(),
            NullLogger<SevenZipCompressor>.Instance
        );

    private static (string EntryPath, Stream Data)[] OneNewEntry() => new[] { ("new.txt", (Stream)new MemoryStream(new byte[] { 1, 2, 3 })) };

    [Test]
    public async Task AppendAsync_CallsUpdateItems_WithExistingPlusNewCount()
    {
        var outArchive = new FakeOutArchive();
        var inArchive = new FakeInArchiveForAppend(count: 3);
        using var compressor = CreateCompressor(outArchive);
        var output = new MemoryStream();

        await compressor.AppendAsync(inArchive, outArchive, 3u, OneNewEntry(), output);

        outArchive.LastCount.Should().Be(4u);
    }

    [Test]
    public async Task AppendAsync_ReturnsFail_WhenParametersInvalid()
    {
        var invalidParams = CompressionParameters.Default with { ThreadCount = 0 };
        using var compressor = CreateCompressor(parameters: invalidParams);
        var output = new MemoryStream();

        var result = await compressor.AppendAsync(new FakeInArchiveForAppend(), new FakeOutArchive(), 1u, OneNewEntry(), output);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task AppendAsync_ReturnsFail_WhenUpdateItemsReturnsError()
    {
        var outArchive = new FakeOutArchive(HResult.NotImplemented);
        using var compressor = CreateCompressor(outArchive);
        var output = new MemoryStream();

        var result = await compressor.AppendAsync(new FakeInArchiveForAppend(), outArchive, 1u, OneNewEntry(), output);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task AppendAsync_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        using var compressor = CreateCompressor();
        var output = new MemoryStream();

        await FluentActions
            .Awaiting(() => compressor.AppendAsync(new FakeInArchiveForAppend(), new FakeOutArchive(), 1u, OneNewEntry(), output, cancellationToken: cts.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    // CompressMultiVolumeAsync — public overload via internal constructor.
    // These call CompressMultiVolumeInternalAsync (including param validation),
    // unlike the old internal-overload tests that called RunUpdateItemsAsync directly.

    [Test]
    public async Task CompressMultiVolumeAsync_CallsUpdateItems_WithCorrectCount()
    {
        var outArchive = new FakeOutArchive();
        using var compressor = CreateCompressor(outArchive);

        await compressor.CompressMultiVolumeAsync(OneNewEntry(), _ => new MemoryStream(), 1024 * 1024);

        outArchive.LastCount.Should().Be(1u);
    }

    [Test]
    public async Task CompressMultiVolumeAsync_ReturnsFail_WhenParametersInvalid()
    {
        var invalidParams = CompressionParameters.Default with { ThreadCount = 0 };
        using var compressor = CreateCompressor(parameters: invalidParams);

        var result = await compressor.CompressMultiVolumeAsync(OneNewEntry(), _ => new MemoryStream(), 1024 * 1024);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task CompressMultiVolumeAsync_ReturnsFail_WhenUpdateItemsReturnsError()
    {
        var outArchive = new FakeOutArchive(HResult.NotImplemented);
        using var compressor = CreateCompressor(outArchive);

        var result = await compressor.CompressMultiVolumeAsync(OneNewEntry(), _ => new MemoryStream(), 1024 * 1024);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task CompressMultiVolumeAsync_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        using var compressor = CreateCompressor();

        await FluentActions
            .Awaiting(() => compressor.CompressMultiVolumeAsync(OneNewEntry(), _ => new MemoryStream(), 1024 * 1024, cancellationToken: cts.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    // AppendAsync — public overload early-exit paths (param validation and dispose guard
    // run before the native archive creation, so no native library is required).

    [Test]
    public async Task AppendAsync_ReturnsOk_WhenUpdateItemsSucceeds()
    {
        var outArchive = new FakeOutArchive();
        using var compressor = CreateCompressor(outArchive);
        var output = new MemoryStream();

        var result = await compressor.AppendAsync(new FakeInArchiveForAppend(), outArchive, 1u, OneNewEntry(), output);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task CompressMultiVolumeAsync_ReturnsOk_WhenUpdateItemsSucceeds()
    {
        var outArchive = new FakeOutArchive();
        using var compressor = CreateCompressor(outArchive);

        var result = await compressor.CompressMultiVolumeAsync(OneNewEntry(), _ => new MemoryStream(), 1024 * 1024);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task AppendAsync_Public_ReturnsFail_WhenParametersInvalid()
    {
        var invalidParams = CompressionParameters.Default with { ThreadCount = 0 };
        using var compressor = CreateCompressor(parameters: invalidParams);

        var result = await compressor.AppendAsync(Stream.Null, OneNewEntry(), new MemoryStream());

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task AppendAsync_Public_ThrowsObjectDisposedException_AfterDispose()
    {
        var compressor = CreateCompressor();
        compressor.Dispose();

        await FluentActions
            .Awaiting(() => compressor.AppendAsync(Stream.Null, OneNewEntry(), new MemoryStream()))
            .Should()
            .ThrowAsync<ObjectDisposedException>();
    }
}
