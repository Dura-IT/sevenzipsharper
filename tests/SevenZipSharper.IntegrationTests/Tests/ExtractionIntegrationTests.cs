using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests
{
    [TestFixture]
    [TestOf(typeof(SevenZipExtractor))]
    public sealed class ExtractionIntegrationTests
    {
        private static readonly byte[] EntryContent = System.Text.Encoding.UTF8.GetBytes("Hello from SevenZipSharper integration tests");

        private static readonly Lazy<Task<byte[]>> _archiveBytes = new(BuildArchiveAsync);

        private static Task<byte[]> BuildArchiveAsync() =>
            IntegrationTestHelpers.BuildArchiveAsync(ArchiveFormat.SevenZip, CompressionParameters.Default, ("test/hello.txt", EntryContent));

        [Test]
        public async Task OpenAsync_ValidArchive_ReturnsSuccessWithArchiveInfo()
        {
            var archive = await _archiveBytes.Value;
            using var extractor = new SevenZipExtractor(new MemoryStream(archive), ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);

            var result = await extractor.OpenAsync();

            result.IsSuccess.Should().BeTrue();
            result.Value.Format.Should().Be(ArchiveFormat.SevenZip);
        }

        [Test]
        public async Task ListEntriesAsync_AfterOpen_ReturnsEntries()
        {
            var archive = await _archiveBytes.Value;
            using var extractor = new SevenZipExtractor(new MemoryStream(archive), ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);
            await extractor.OpenAsync();

            var result = await extractor.ListEntriesAsync();

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(1);
            result.Value[0].Path.Should().Be("test/hello.txt");
            result.Value[0].Size.Should().Be((ulong)EntryContent.Length);
        }

        [Test]
        public async Task ExtractAllAsync_AfterOpen_WritesFilesWithCorrectContent()
        {
            var archive = await _archiveBytes.Value;
            using var extractor = new SevenZipExtractor(new MemoryStream(archive), ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);
            await extractor.OpenAsync();

            var outDir = IntegrationTestHelpers.UniqueTempDir("extractAll");
            try
            {
                var result = await extractor.ExtractAllAsync(outDir);

                result.IsSuccess.Should().BeTrue();
                var extracted = Path.Combine(outDir, "test", "hello.txt");
                File.Exists(extracted).Should().BeTrue();
                (await File.ReadAllBytesAsync(extracted)).Should().BeEquivalentTo(EntryContent);
            }
            finally
            {
                if (Directory.Exists(outDir))
                    Directory.Delete(outDir, recursive: true);
            }
        }

        [Test]
        public async Task ExtractEntryAsync_AfterOpen_WritesCorrectContent()
        {
            var archive = await _archiveBytes.Value;
            using var extractor = new SevenZipExtractor(new MemoryStream(archive), ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);
            await extractor.OpenAsync();
            var entries = (await extractor.ListEntriesAsync()).Value;
            using var output = new MemoryStream();

            var result = await extractor.ExtractEntryAsync(entries[0], output);

            result.IsSuccess.Should().BeTrue();
            output.ToArray().Should().BeEquivalentTo(EntryContent);
        }

        [Test]
        public async Task ExtractAllAsync_WithSmallFlushInterval_WritesCompleteContent()
        {
            // Payload larger than the flush interval so the periodic flush-to-disk path fires
            // repeatedly through the real native decompression callback; verifies pacing does
            // not truncate or corrupt the output.
            var payload = new byte[256 * 1024];
            for (var i = 0; i < payload.Length; i++)
                payload[i] = (byte)(i % 251);
            var archive = await IntegrationTestHelpers.BuildArchiveAsync(ArchiveFormat.SevenZip, CompressionParameters.Default, ("big/payload.bin", payload));
            using var extractor = new SevenZipExtractor(new MemoryStream(archive), ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);
            await extractor.OpenAsync();

            var outDir = IntegrationTestHelpers.UniqueTempDir("extractFlush");
            try
            {
                var options = new ExtractionOptions { FlushIntervalBytes = 64 * 1024 };
                var result = await extractor.ExtractAllAsync(outDir, options: options);

                result.IsSuccess.Should().BeTrue();
                var extracted = Path.Combine(outDir, "big", "payload.bin");
                File.Exists(extracted).Should().BeTrue();
                (await File.ReadAllBytesAsync(extracted)).Should().Equal(payload);
            }
            finally
            {
                if (Directory.Exists(outDir))
                    Directory.Delete(outDir, recursive: true);
            }
        }

        [Test]
        public async Task ExtractAllAsync_ManySmallFilesBelowFlushInterval_WritesAllContent()
        {
            // Each entry is smaller than the flush interval, but the entries together exceed it. A
            // per-file pacer would never flush; the shared pacer must accumulate across entries and
            // pace without truncating or corrupting any of the small files.
            const int fileCount = 32;
            const int fileSize = 8 * 1024;
            var entries = new (string Path, byte[] Content)[fileCount];
            for (var f = 0; f < fileCount; f++)
            {
                var payload = new byte[fileSize];
                for (var i = 0; i < payload.Length; i++)
                    payload[i] = (byte)((i + f) % 251);
                entries[f] = ($"small/file-{f:D2}.bin", payload);
            }
            var archive = await IntegrationTestHelpers.BuildArchiveAsync(ArchiveFormat.SevenZip, CompressionParameters.Default, entries);
            using var extractor = new SevenZipExtractor(new MemoryStream(archive), ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);
            await extractor.OpenAsync();

            var outDir = IntegrationTestHelpers.UniqueTempDir("extractManySmall");
            try
            {
                // 16 KiB interval: below the 256 KiB total, above any single 8 KiB file.
                var options = new ExtractionOptions { FlushIntervalBytes = 16 * 1024 };
                var result = await extractor.ExtractAllAsync(outDir, options: options);

                result.IsSuccess.Should().BeTrue();
                foreach (var (path, content) in entries)
                {
                    var extracted = Path.Combine(outDir, path.Replace('/', Path.DirectorySeparatorChar));
                    File.Exists(extracted).Should().BeTrue();
                    (await File.ReadAllBytesAsync(extracted)).Should().Equal(content);
                }
            }
            finally
            {
                if (Directory.Exists(outDir))
                    Directory.Delete(outDir, recursive: true);
            }
        }
    }
}
