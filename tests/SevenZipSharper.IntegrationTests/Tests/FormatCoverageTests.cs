using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests;

[TestFixture]
[TestOf(typeof(SevenZipCompressor))]
public sealed class FormatCoverageTests
{
    private static readonly byte[] Content = System.Text.Encoding.UTF8.GetBytes(
        "Format coverage test content"
    );

    [TestCase(ArchiveFormat.SevenZip)]
    [TestCase(ArchiveFormat.Zip)]
    public async Task Compress_ThenExtract_RoundTripSucceeds(ArchiveFormat format)
    {
        var entries = new (string, Stream)[] { ("file.txt", new MemoryStream(Content)) };

        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                format,
                CompressionParameters.Default,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var compressResult = await compressor.CompressAsync(entries, archive);
            compressResult.IsSuccess.Should().BeTrue($"compression to {format} should succeed");
        }

        archive.Position = 0;
        archive.Length.Should().BeGreaterThan(0, $"{format} archive should not be empty");

        using var extractor = new SevenZipExtractor(
            archive,
            format,
            NullLogger<SevenZipExtractor>.Instance
        );
        var openResult = await extractor.OpenAsync();
        openResult.IsSuccess.Should().BeTrue($"opening {format} archive should succeed");

        var entriesResult = await extractor.ListEntriesAsync();
        entriesResult.IsSuccess.Should().BeTrue();
        entriesResult.Value.Should().HaveCount(1);

        using var output = new MemoryStream();
        var extractResult = await extractor.ExtractEntryAsync(entriesResult.Value[0], output);
        extractResult.IsSuccess.Should().BeTrue();
        output.ToArray().Should().BeEquivalentTo(Content);
    }

    [TestCase(ArchiveFormat.SevenZip)]
    [TestCase(ArchiveFormat.Zip)]
    public async Task ListEntriesAsync_ArchiveWithMultipleEntries_ReturnsCorrectMetadata(
        ArchiveFormat format
    )
    {
        var entries = new (string, Stream)[]
        {
            ("docs/readme.txt", new MemoryStream(new byte[] { 1, 2, 3 })),
            ("src/main.cs", new MemoryStream(new byte[] { 4, 5, 6, 7 })),
        };

        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                format,
                CompressionParameters.Default,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            await compressor.CompressAsync(entries, archive);
        }

        archive.Position = 0;
        using var extractor = new SevenZipExtractor(
            archive,
            format,
            NullLogger<SevenZipExtractor>.Instance
        );
        await extractor.OpenAsync();

        var result = await extractor.ListEntriesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(e => e.Path == "docs/readme.txt");
        result.Value.Should().Contain(e => e.Path == "src/main.cs");
    }
}
