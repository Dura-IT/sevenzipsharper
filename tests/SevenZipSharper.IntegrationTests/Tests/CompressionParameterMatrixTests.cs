using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests;

/// <summary>
/// Exhaustive matrix of (format × method × level × threadCount) combinations the library claims to support.
/// Each combination roundtrips a 64 KB highly-compressible payload through a fresh compressor/extractor pair
/// and verifies the extracted bytes match the input exactly. Combinations not asserted here are either
/// covered by <see cref="FormatFallbackBehaviorTests"/> (silent fallback paths) or known to be unsupported.
/// </summary>
[TestFixture]
[TestOf(typeof(SevenZipCompressor))]
public sealed class CompressionParameterMatrixTests
{
    private static readonly byte[] CompressibleContent = BuildCompressibleContent();

    private static byte[] BuildCompressibleContent()
    {
        // 64 KB highly compressible — LZMA/LZMA2 reduces this well below 1 KB; Copy preserves the size.
        var data = new byte[64 * 1024];
        Array.Fill(data, (byte)0xAA);
        return data;
    }

    public static IEnumerable<TestCaseData> MatrixCases()
    {
        var sevenZipMethods = new[]
        {
            CompressionMethod.Lzma,
            CompressionMethod.Lzma2,
            CompressionMethod.BZip2,
            CompressionMethod.Ppmd,
            CompressionMethod.Deflate,
            CompressionMethod.Copy,
        };

        var levels = new[]
        {
            CompressionLevel.Fastest,
            CompressionLevel.Fast,
            CompressionLevel.Normal,
            CompressionLevel.Maximum,
            CompressionLevel.Ultra,
        };

        // 7z × every method × every level (30 cases)
        foreach (var method in sevenZipMethods)
        {
            foreach (var level in levels)
            {
                yield return new TestCaseData(ArchiveFormat.SevenZip, method, level, null).SetName(
                    $"7z_{method}_{level}"
                );
            }
        }

        // 7z × Copy × Store — uncompressed storage path
        yield return new TestCaseData(
            ArchiveFormat.SevenZip,
            CompressionMethod.Copy,
            CompressionLevel.Store,
            null
        ).SetName("7z_Copy_Store");

        // 7z × Lzma2 × varying thread counts — exercises the multi-thread codec path
        yield return new TestCaseData(
            ArchiveFormat.SevenZip,
            CompressionMethod.Lzma2,
            CompressionLevel.Normal,
            (int?)1
        ).SetName("7z_Lzma2_Normal_1Thread");
        yield return new TestCaseData(
            ArchiveFormat.SevenZip,
            CompressionMethod.Lzma2,
            CompressionLevel.Normal,
            (int?)2
        ).SetName("7z_Lzma2_Normal_2Threads");
        yield return new TestCaseData(
            ArchiveFormat.SevenZip,
            CompressionMethod.Lzma2,
            CompressionLevel.Normal,
            (int?)4
        ).SetName("7z_Lzma2_Normal_4Threads");

        // Zip × supported methods × every level (10 cases)
        var zipMethods = new[] { CompressionMethod.Deflate, CompressionMethod.BZip2 };
        foreach (var method in zipMethods)
        {
            foreach (var level in levels)
            {
                yield return new TestCaseData(ArchiveFormat.Zip, method, level, null).SetName(
                    $"Zip_{method}_{level}"
                );
            }
        }

        // Zip × Deflate × Store — stored entries (level 0 disables compression on the codec)
        yield return new TestCaseData(
            ArchiveFormat.Zip,
            CompressionMethod.Deflate,
            CompressionLevel.Store,
            null
        ).SetName("Zip_Deflate_Store");
    }

    [TestCaseSource(nameof(MatrixCases))]
    public async Task Compress_ThenExtract_RoundTripSucceeds(
        ArchiveFormat format,
        CompressionMethod method,
        CompressionLevel level,
        int? threadCount
    )
    {
        var parameters = new CompressionParameters
        {
            Method = method,
            Level = level,
            ThreadCount = threadCount,
        };

        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                format,
                parameters,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var entries = new[] { ("payload.bin", (Stream)new MemoryStream(CompressibleContent)) };
            var result = await compressor.CompressAsync(entries, archive);
            result
                .IsSuccess.Should()
                .BeTrue(
                    $"{format}/{method}/{level} (threads={threadCount?.ToString() ?? "default"}) compression failed: {string.Join("; ", result.Errors)}"
                );
        }

        archive
            .Length.Should()
            .BeGreaterThan(0, $"{format}/{method}/{level} archive should not be empty");

        archive.Position = 0;
        using var extractor = new SevenZipExtractor(
            archive,
            format,
            NullLogger<SevenZipExtractor>.Instance
        );

        (await extractor.OpenAsync()).IsSuccess.Should().BeTrue($"{format}/{method}/{level} open");
        var entriesResult = await extractor.ListEntriesAsync();
        entriesResult.IsSuccess.Should().BeTrue();
        entriesResult.Value.Should().HaveCount(1);

        using var output = new MemoryStream();
        (await extractor.ExtractEntryAsync(entriesResult.Value[0], output))
            .IsSuccess.Should()
            .BeTrue($"{format}/{method}/{level} extract");

        output
            .ToArray()
            .Should()
            .BeEquivalentTo(
                CompressibleContent,
                $"{format}/{method}/{level} content should round-trip byte-for-byte"
            );
    }

    /// <summary>
    /// Regression test for F1 (wchar_t encoding bug in SevenZipWideString).
    /// Validates that the Copy method name reaches the native side correctly — when it does,
    /// the archive contains uncompressed data and is approximately the input size. If the method
    /// name were corrupted, 7-Zip would silently fall back to LZMA2 and the archive would be tiny.
    /// </summary>
    [Test]
    public async Task SevenZip_CopyMethod_ProducesUncompressedArchive()
    {
        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                ArchiveFormat.SevenZip,
                CompressionParameters.Store,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var entries = new[] { ("data.bin", (Stream)new MemoryStream(CompressibleContent)) };
            (await compressor.CompressAsync(entries, archive)).IsSuccess.Should().BeTrue();
        }

        archive
            .Length.Should()
            .BeGreaterThan(
                CompressibleContent.Length / 2,
                "Copy method should produce ~uncompressed archive — a small archive indicates LZMA2 fallback (F1 regression)"
            );
    }

    public static IEnumerable<uint> DictionarySizes()
    {
        yield return 1u * 1024 * 1024; // 1 MB
        yield return 4u * 1024 * 1024; // 4 MB
        yield return 64u * 1024 * 1024; // 64 MB
        yield return 128u * 1024 * 1024; // 128 MB
    }

    /// <summary>
    /// Regression for #12: an explicit DictionarySize was passed to 7-Zip as a numeric PROPVARIANT,
    /// which 7-Zip reads as a log2 exponent and rejects with E_INVALIDARG for any real byte count.
    /// Every value here failed identically before the fix; all must now round-trip.
    /// </summary>
    [TestCaseSource(nameof(DictionarySizes))]
    public async Task SevenZip_WithExplicitDictionarySize_RoundTripSucceeds(uint dictionarySize)
    {
        var parameters = new CompressionParameters
        {
            Method = CompressionMethod.Lzma2,
            Level = CompressionLevel.Ultra,
            DictionarySize = dictionarySize,
        };

        await AssertRoundTripsAsync(ArchiveFormat.SevenZip, parameters);
    }

    /// <summary>
    /// Regression for #12: the built-in MaximumLzma2 preset sets DictionarySize = 128 MB, so the
    /// preset advertised as "smallest possible 7z archives" was completely unusable before the fix.
    /// </summary>
    [Test]
    public async Task MaximumLzma2Preset_RoundTripSucceeds()
    {
        await AssertRoundTripsAsync(ArchiveFormat.SevenZip, CompressionParameters.MaximumLzma2);
    }

    private static async Task AssertRoundTripsAsync(
        ArchiveFormat format,
        CompressionParameters parameters
    )
    {
        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                format,
                parameters,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var entries = new[] { ("payload.bin", (Stream)new MemoryStream(CompressibleContent)) };
            var result = await compressor.CompressAsync(entries, archive);
            result
                .IsSuccess.Should()
                .BeTrue($"compression failed: {string.Join("; ", result.Errors)}");
        }

        archive.Position = 0;
        using var extractor = new SevenZipExtractor(
            archive,
            format,
            NullLogger<SevenZipExtractor>.Instance
        );

        (await extractor.OpenAsync()).IsSuccess.Should().BeTrue("open");
        var entriesResult = await extractor.ListEntriesAsync();
        entriesResult.IsSuccess.Should().BeTrue();
        entriesResult.Value.Should().HaveCount(1);

        using var output = new MemoryStream();
        (await extractor.ExtractEntryAsync(entriesResult.Value[0], output))
            .IsSuccess.Should()
            .BeTrue("extract");

        output.ToArray().Should().BeEquivalentTo(CompressibleContent, "content should round-trip");
    }
}
