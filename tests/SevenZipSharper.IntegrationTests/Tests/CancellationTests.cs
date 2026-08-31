using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests
{
    [TestFixture]
    [TestOf(typeof(SevenZipExtractor))]
    public sealed class CancellationTests
    {
        private static readonly Lazy<Task<byte[]>> _archiveBytes = new(BuildArchiveAsync);

        private static async Task<byte[]> BuildArchiveAsync()
        {
            var content = new byte[128 * 1024];
            new Random(0).NextBytes(content);
            return await IntegrationTestHelpers.BuildArchiveAsync(ArchiveFormat.SevenZip, CompressionParameters.Default, ("large.bin", content));
        }

        [Test]
        public async Task ExtractAllAsync_PreCancelledToken_ThrowsOperationCanceledException()
        {
            var archive = await _archiveBytes.Value;
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            using var extractor = new SevenZipExtractor(new MemoryStream(archive), ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);
            await extractor.OpenAsync();

            var outDir = IntegrationTestHelpers.UniqueTempDir("cancel_extractAll");
            try
            {
                await FluentActions
                    .Awaiting(() => extractor.ExtractAllAsync(outDir, cancellationToken: cts.Token))
                    .Should()
                    .ThrowAsync<OperationCanceledException>();
            }
            finally
            {
                if (Directory.Exists(outDir))
                    Directory.Delete(outDir, recursive: true);
            }
        }

        [Test]
        public async Task ListEntriesAsync_PreCancelledToken_ThrowsOperationCanceledException()
        {
            var archive = await _archiveBytes.Value;
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            using var extractor = new SevenZipExtractor(new MemoryStream(archive), ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);
            await extractor.OpenAsync();

            await FluentActions.Awaiting(() => extractor.ListEntriesAsync(cts.Token)).Should().ThrowAsync<OperationCanceledException>();
        }
    }
}
