using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests;

[TestFixture]
[TestOf(typeof(SevenZipCompressor))]
public sealed class CompressionRoundTripTests
{
    [Test]
    public async Task CompressAsync_ThenExtract_ContentMatchesOriginal()
    {
        var original = System.Text.Encoding.UTF8.GetBytes("Round-trip test content — SevenZipSharper");
        var entries = new (string, Stream)[] { ("roundtrip.txt", new MemoryStream(original)) };

        using var archive = new MemoryStream();
        using (var compressor = new SevenZipCompressor(ArchiveFormat.SevenZip, CompressionParameters.Default, NullLogger<SevenZipCompressor>.Instance))
        {
            var compressResult = await compressor.CompressAsync(entries, archive);
            compressResult.IsSuccess.Should().BeTrue();
        }

        archive.Position = 0;
        using var extractor = new SevenZipExtractor(archive, ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);
        await extractor.OpenAsync();
        var entryList = (await extractor.ListEntriesAsync()).Value;
        using var output = new MemoryStream();
        await extractor.ExtractEntryAsync(entryList[0], output);

        output.ToArray().Should().BeEquivalentTo(original);
    }

    [Test]
    public async Task CompressAsync_MultipleEntries_AllRoundTripCorrectly()
    {
        var entries = new (string, Stream)[]
        {
            ("alpha.txt", new MemoryStream(new byte[] { 1, 2, 3 })),
            ("beta.txt", new MemoryStream(new byte[] { 4, 5, 6, 7, 8 })),
            ("gamma.txt", new MemoryStream(new byte[] { 9 })),
        };

        using var archive = new MemoryStream();
        using (var compressor = new SevenZipCompressor(ArchiveFormat.SevenZip, CompressionParameters.Default, NullLogger<SevenZipCompressor>.Instance))
        {
            (await compressor.CompressAsync(entries, archive)).IsSuccess.Should().BeTrue();
        }

        archive.Position = 0;
        using var extractor = new SevenZipExtractor(archive, ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);
        await extractor.OpenAsync();
        var entryList = (await extractor.ListEntriesAsync()).Value;

        entryList.Should().HaveCount(3);

        foreach (var entry in entryList)
        {
            using var buf = new MemoryStream();
            (await extractor.ExtractEntryAsync(entry, buf)).IsSuccess.Should().BeTrue();
            buf.Length.Should().BeGreaterThan(0);
        }
    }

    [Test]
    public async Task CompressAsync_WithProgress_ReportsProgressEvents()
    {
        var progressReports = new System.Collections.Generic.List<CompressionProgress>();
        var progress = new SynchronousProgress<CompressionProgress>(p => progressReports.Add(p));
        var content = new byte[64 * 1024];
        new Random(42).NextBytes(content);
        var entries = new (string, Stream)[] { ("big.bin", new MemoryStream(content)) };

        using var archive = new MemoryStream();
        using var compressor = new SevenZipCompressor(ArchiveFormat.SevenZip, CompressionParameters.Default, NullLogger<SevenZipCompressor>.Instance);
        await compressor.CompressAsync(entries, archive, progress);

        progressReports.Should().NotBeEmpty();
    }
}

file sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _callback;

    internal SynchronousProgress(Action<T> callback) => _callback = callback;

    public void Report(T value) => _callback(value);
}
