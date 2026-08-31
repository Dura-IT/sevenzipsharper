using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.UnitTests;

[TestOf(typeof(SevenZipCompressor))]
public sealed class SevenZipCompressorTests
{
    private static SevenZipCompressor CreateCompressor(
        IOutArchive? archive = null,
        CompressionParameters? parameters = null,
        ArchiveFormat format = ArchiveFormat.SevenZip
    )
    {
        archive ??= new FakeOutArchive();
        return new SevenZipCompressor(format, parameters ?? CompressionParameters.Default, archive, NullLogger<SevenZipCompressor>.Instance);
    }

    private static readonly byte[] _sampleContent = { 1, 2, 3 };
    private static readonly string[] _singleFilePath = { "anything" };

    private static (string EntryPath, Stream Data)[] OneEntry() => new[] { ("file.txt", (Stream)new MemoryStream(_sampleContent)) };

    [Test]
    public async Task CompressAsync_CallsUpdateItems_WithCorrectCount()
    {
        var archive = new FakeOutArchive();
        using var compressor = CreateCompressor(archive);
        var output = new MemoryStream();

        await compressor.CompressAsync(OneEntry(), output);

        archive.LastCount.Should().Be(1u);
    }

    [Test]
    public async Task CompressAsync_ReturnsFail_WhenParametersInvalid()
    {
        var invalidParams = CompressionParameters.Default with { ThreadCount = 0 };
        using var compressor = CreateCompressor(parameters: invalidParams);
        var output = new MemoryStream();

        var result = await compressor.CompressAsync(OneEntry(), output);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task CompressAsync_ReturnsFail_WhenUpdateItemsReturnsError()
    {
        var archive = new FakeOutArchive(HResult.NotImplemented);
        using var compressor = CreateCompressor(archive);
        var output = new MemoryStream();

        var result = await compressor.CompressAsync(OneEntry(), output);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task CompressAsync_ReturnsOk_WhenUpdateItemsSucceeds()
    {
        var archive = new FakeOutArchive();
        using var compressor = CreateCompressor(archive);
        var output = new MemoryStream();

        var result = await compressor.CompressAsync(OneEntry(), output);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task CompressAsync_EncryptHeadersOnZip_ReturnsFailure()
    {
        var parameters = CompressionParameters.Default with { EncryptionPassword = "secret", EncryptHeaders = true };
        using var compressor = CreateCompressor(parameters: parameters, format: ArchiveFormat.Zip);

        var result = await compressor.CompressAsync(OneEntry(), new MemoryStream());

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("EncryptHeaders"));
    }

    [Test]
    public async Task CompressAsync_PasswordOnTar_ReturnsFailure()
    {
        var parameters = CompressionParameters.Default with { EncryptionPassword = "secret" };
        using var compressor = CreateCompressor(parameters: parameters, format: ArchiveFormat.Tar);

        var result = await compressor.CompressAsync(OneEntry(), new MemoryStream());

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("Encryption"));
    }

    [Test]
    public async Task CompressAsync_PasswordOnGZip_ReturnsFailure()
    {
        var parameters = CompressionParameters.Default with { EncryptionPassword = "secret" };
        using var compressor = CreateCompressor(parameters: parameters, format: ArchiveFormat.GZip);

        var result = await compressor.CompressAsync(OneEntry(), new MemoryStream());

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("Encryption"));
    }

    [Test]
    public async Task CompressAsync_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        using var compressor = CreateCompressor();
        var output = new MemoryStream();

        await FluentActions
            .Awaiting(() => compressor.CompressAsync(OneEntry(), output, cancellationToken: cts.Token))
            .Should()
            .ThrowAsync<System.OperationCanceledException>();
    }

    [TestCase(ArchiveFormat.BZip2)]
    [TestCase(ArchiveFormat.Xz)]
    public async Task CompressAsync_PasswordOnUnsupportedFormat_ReturnsFailure(ArchiveFormat format)
    {
        var parameters = CompressionParameters.Default with { EncryptionPassword = "secret" };
        using var compressor = CreateCompressor(parameters: parameters, format: format);

        var result = await compressor.CompressAsync(OneEntry(), new MemoryStream());

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("Encryption"));
    }

    [TestCase(ArchiveFormat.Zip)]
    [TestCase(ArchiveFormat.Tar)]
    [TestCase(ArchiveFormat.GZip)]
    [TestCase(ArchiveFormat.BZip2)]
    [TestCase(ArchiveFormat.Xz)]
    public async Task CompressAsync_EncryptHeadersOnNonSevenZipFormat_ReturnsFailure(ArchiveFormat format)
    {
        var parameters = CompressionParameters.Default with { EncryptionPassword = "secret", EncryptHeaders = true };
        using var compressor = CreateCompressor(parameters: parameters, format: format);

        var result = await compressor.CompressAsync(OneEntry(), new MemoryStream());

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("EncryptHeaders"));
    }

    [Test]
    public async Task CompressAsync_PasswordOnSevenZip_Succeeds()
    {
        var parameters = CompressionParameters.Default with { EncryptionPassword = "secret" };
        using var compressor = CreateCompressor(parameters: parameters);

        var result = await compressor.CompressAsync(OneEntry(), new MemoryStream());

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task CompressAsync_PasswordOnZip_Succeeds()
    {
        var parameters = CompressionParameters.Default with { EncryptionPassword = "secret" };
        using var compressor = CreateCompressor(parameters: parameters, format: ArchiveFormat.Zip);

        var result = await compressor.CompressAsync(OneEntry(), new MemoryStream());

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var compressor = CreateCompressor();

        compressor.Dispose();
        var act = () => compressor.Dispose();

        act.Should().NotThrow();
    }

    [Test]
    public async Task CompressAsync_ThrowsObjectDisposedException_AfterDispose()
    {
        var compressor = CreateCompressor();
        compressor.Dispose();

        await FluentActions.Awaiting(() => compressor.CompressAsync(OneEntry(), new MemoryStream())).Should().ThrowAsync<System.ObjectDisposedException>();
    }

    [Test]
    public async Task CompressFilesAsync_ThrowsObjectDisposedException_AfterDispose()
    {
        var compressor = CreateCompressor();
        compressor.Dispose();

        await FluentActions
            .Awaiting(() => compressor.CompressFilesAsync(_singleFilePath, Path.GetTempPath(), new MemoryStream()))
            .Should()
            .ThrowAsync<System.ObjectDisposedException>();
    }

    [Test]
    public async Task CompressMultiVolumeAsync_ThrowsObjectDisposedException_AfterDispose()
    {
        var compressor = CreateCompressor();
        compressor.Dispose();

        await FluentActions
            .Awaiting(() => compressor.CompressMultiVolumeAsync(OneEntry(), _ => new MemoryStream(), 1024))
            .Should()
            .ThrowAsync<System.ObjectDisposedException>();
    }

    [Test]
    public async Task CompressMultiVolumeAsync_ThrowsArgumentNullException_WhenFactoryNull()
    {
        using var compressor = CreateCompressor();

        await FluentActions
            .Awaiting(() => compressor.CompressMultiVolumeAsync(OneEntry(), null!, 1024))
            .Should()
            .ThrowAsync<System.ArgumentNullException>()
            .WithParameterName("volumeStreamFactory");
    }
}

[GeneratedComClass]
internal sealed partial class FakeOutArchive : IOutArchive
{
    private readonly int _hResult;

    internal FakeOutArchive(int hResult = HResult.Ok)
    {
        _hResult = hResult;
    }

    public uint LastCount { get; private set; }

    public int UpdateItems(IOutStream outStream, uint numItems, IArchiveUpdateCallback updateCallback)
    {
        LastCount = numItems;
        return _hResult;
    }

    public int GetFileTimeType(out uint type)
    {
        type = 0;
        return HResult.Ok;
    }
}
