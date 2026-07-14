using System;
using System.Collections.Generic;
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
public sealed class SevenZipCompressorFilesTests
{
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static SevenZipCompressor CreateCompressor(
        IOutArchive? archive = null,
        CompressionParameters? parameters = null
    ) =>
        new SevenZipCompressor(
            ArchiveFormat.SevenZip,
            parameters ?? CompressionParameters.Default,
            archive ?? new FakeOutArchive(),
            NullLogger<SevenZipCompressor>.Instance
        );

    private string CreateTempFile(string relativePath, byte[]? content = null)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content ?? new byte[] { 1, 2, 3 });
        return fullPath;
    }

    [Test]
    public async Task CompressFilesAsync_CallsUpdateItems_WithCorrectFileCount()
    {
        var file1 = CreateTempFile("a.txt");
        var file2 = CreateTempFile("b.txt");
        var archive = new FakeOutArchive();
        using var compressor = CreateCompressor(archive);
        var output = new MemoryStream();

        await compressor.CompressFilesAsync(new[] { file1, file2 }, _tempDir, output);

        archive.LastCount.Should().Be(2u);
    }

    [Test]
    public async Task CompressFilesAsync_ComputesEntryPaths_RelativeToBasePath()
    {
        var file = CreateTempFile(Path.Combine("sub", "file.txt"));
        var archive = new FakeOutArchiveCapturingPaths();
        using var compressor = CreateCompressor(archive);
        var output = new MemoryStream();

        await compressor.CompressFilesAsync(new[] { file }, _tempDir, output);

        archive
            .CapturedPaths.Should()
            .ContainSingle()
            .Which.Should()
            .Be(Path.Combine("sub", "file.txt"));
    }

    [Test]
    public async Task CompressFilesAsync_ReturnsOk_WhenUpdateItemsSucceeds()
    {
        var file = CreateTempFile("ok.txt");
        using var compressor = CreateCompressor(new FakeOutArchive());
        var output = new MemoryStream();

        var result = await compressor.CompressFilesAsync(new[] { file }, _tempDir, output);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task CompressFilesAsync_ReturnsFail_WhenParametersInvalid()
    {
        var file = CreateTempFile("c.txt");
        var invalidParams = CompressionParameters.Default with { ThreadCount = 0 };
        using var compressor = CreateCompressor(parameters: invalidParams);
        var output = new MemoryStream();

        var result = await compressor.CompressFilesAsync(new[] { file }, _tempDir, output);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public void CompressFilesAsync_ThrowsFileNotFoundException_WhenFileDoesNotExist()
    {
        var missingPath = Path.Combine(_tempDir, "does-not-exist.txt");
        using var compressor = CreateCompressor();
        var output = new MemoryStream();

        Func<Task> act = () =>
            compressor.CompressFilesAsync(new[] { missingPath }, _tempDir, output);

        act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Test]
    public async Task CompressFilesAsync_PropagatesCancellation()
    {
        var file = CreateTempFile("d.txt");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        using var compressor = CreateCompressor();
        var output = new MemoryStream();

        await FluentActions
            .Awaiting(() =>
                compressor.CompressFilesAsync(
                    new[] { file },
                    _tempDir,
                    output,
                    cancellationToken: cts.Token
                )
            )
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }
}

[GeneratedComClass]
internal sealed partial class FakeOutArchiveCapturingPaths : IOutArchive
{
    internal uint LastCount { get; private set; }
    internal List<string> CapturedPaths { get; } = [];

    public int UpdateItems(
        IOutStream outStream,
        uint numItems,
        IArchiveUpdateCallback updateCallback
    )
    {
        LastCount = numItems;
        for (uint i = 0; i < numItems; i++)
        {
            var prop = new PropVariant();
            updateCallback.GetProperty(i, ItemPropId.Path, ref prop);
            CapturedPaths.Add(prop.ToStringValue() ?? string.Empty);
        }
        return HResult.Ok;
    }

    public int GetFileTimeType(out uint type)
    {
        type = 0;
        return HResult.Ok;
    }
}
