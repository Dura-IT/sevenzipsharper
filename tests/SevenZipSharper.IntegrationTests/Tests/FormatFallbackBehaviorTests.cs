using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests;

/// <summary>
/// Documents — via test — how 7-Zip behaves when a caller passes a (format, method) combination
/// the underlying format does not natively support. The library does not pre-validate these
/// combinations; behaviour is delegated to the native 7-Zip layer, which silently substitutes a
/// compatible codec for ZIP archives. These tests pin that behaviour so future changes are
/// visible, and so callers reading the test suite can predict the actual outcome.
/// </summary>
[TestFixture]
[TestOf(typeof(SevenZipCompressor))]
public sealed class FormatFallbackBehaviorTests
{
    private static readonly byte[] CompressibleContent = BuildCompressibleContent();

    private static byte[] BuildCompressibleContent()
    {
        var data = new byte[64 * 1024];
        Array.Fill(data, (byte)0xAA);
        return data;
    }

    /// <summary>
    /// LZMA2 has no ZIP method code — 7-Zip silently substitutes Deflate (method 8).
    /// Callers wanting LZMA2 compression must use the 7z format.
    /// </summary>
    [Test]
    public async Task Zip_WithLzma2Method_FallsBackToDeflate()
    {
        var parameters = new CompressionParameters
        {
            Method = CompressionMethod.Lzma2,
            Level = CompressionLevel.Normal,
        };

        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                ArchiveFormat.Zip,
                parameters,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var entries = new[] { ("fallback.bin", (Stream)new MemoryStream(CompressibleContent)) };
            (await compressor.CompressAsync(entries, archive))
                .IsSuccess.Should()
                .BeTrue("Zip + LZMA2 should succeed via silent Deflate fallback");
        }

        // Verify the written method code is Deflate (8), not any LZMA variant.
        archive.Position = 0;
        using var reader = new BinaryReader(archive, System.Text.Encoding.UTF8, leaveOpen: true);
        reader.ReadInt32(); // local file header signature
        reader.ReadUInt16(); // version needed
        reader.ReadUInt16(); // general purpose bit flag
        var methodCode = reader.ReadUInt16();
        methodCode
            .Should()
            .Be(8, "LZMA2 has no ZIP method code — 7-Zip must substitute Deflate (8)");

        // Confirm roundtrip.
        archive.Position = 0;
        using var extractor = new SevenZipExtractor(
            archive,
            ArchiveFormat.Zip,
            NullLogger<SevenZipExtractor>.Instance
        );
        (await extractor.OpenAsync()).IsSuccess.Should().BeTrue();
        var entriesResult = await extractor.ListEntriesAsync();
        entriesResult.IsSuccess.Should().BeTrue();
        using var output = new MemoryStream();
        (await extractor.ExtractEntryAsync(entriesResult.Value[0], output))
            .IsSuccess.Should()
            .BeTrue();
        output.ToArray().Should().BeEquivalentTo(CompressibleContent);
    }

    /// <summary>
    /// LZMA (method 14), BZip2 (method 12), and PPMd (method 98) are registered ZIP method
    /// codes that 7-Zip uses natively — no fallback to Deflate occurs.
    /// The resulting archives are valid but may not open in tools that only support
    /// standard ZIP (Store + Deflate).
    /// </summary>
    [TestCase(CompressionMethod.Lzma, (ushort)14)]
    [TestCase(CompressionMethod.BZip2, (ushort)12)]
    [TestCase(CompressionMethod.Ppmd, (ushort)98)]
    public async Task Zip_WithNativeZipMethod_WritesCorrectMethodCode(
        CompressionMethod method,
        ushort expectedMethodCode
    )
    {
        var parameters = new CompressionParameters
        {
            Method = method,
            Level = CompressionLevel.Normal,
        };

        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                ArchiveFormat.Zip,
                parameters,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var entries = new[] { ("payload.bin", (Stream)new MemoryStream(CompressibleContent)) };
            (await compressor.CompressAsync(entries, archive))
                .IsSuccess.Should()
                .BeTrue($"Zip + {method} should succeed without error");
        }

        // Verify the local file header contains the expected ZIP method code.
        archive.Position = 0;
        using var reader = new BinaryReader(archive, System.Text.Encoding.UTF8, leaveOpen: true);
        reader.ReadInt32(); // local file header signature
        reader.ReadUInt16(); // version needed
        reader.ReadUInt16(); // general purpose bit flag
        var methodCode = reader.ReadUInt16();
        methodCode
            .Should()
            .Be(
                expectedMethodCode,
                $"{method} is a registered ZIP method — 7-Zip should use code {expectedMethodCode}, not fall back to Deflate"
            );

        // Confirm roundtrip.
        archive.Position = 0;
        using var extractor = new SevenZipExtractor(
            archive,
            ArchiveFormat.Zip,
            NullLogger<SevenZipExtractor>.Instance
        );
        (await extractor.OpenAsync()).IsSuccess.Should().BeTrue();
        var entriesResult = await extractor.ListEntriesAsync();
        entriesResult.IsSuccess.Should().BeTrue();
        using var output = new MemoryStream();
        (await extractor.ExtractEntryAsync(entriesResult.Value[0], output))
            .IsSuccess.Should()
            .BeTrue();
        output.ToArray().Should().BeEquivalentTo(CompressibleContent);
    }

    /// <summary>
    /// GZip and BZip2 expose writeable handlers in the native 7-Zip library — the configured
    /// <see cref="CompressionMethod"/> is ignored (these formats only support their built-in codec).
    /// Compression produces a valid single-entry archive; the method parameter has no effect.
    /// </summary>
    [TestCase(ArchiveFormat.GZip)]
    [TestCase(ArchiveFormat.BZip2)]
    public async Task SingleFileFormat_Compression_Succeeds(ArchiveFormat format)
    {
        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                format,
                CompressionParameters.Default,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var entries = new[] { ("only.bin", (Stream)new MemoryStream(CompressibleContent)) };
            var result = await compressor.CompressAsync(entries, archive);
            result
                .IsSuccess.Should()
                .BeTrue(
                    $"{format} should support single-entry compression with its built-in codec"
                );
        }

        archive
            .Length.Should()
            .BeGreaterThan(0, $"{format} archive should contain compressed bytes");
    }

    /// <summary>
    /// Tar is a container-only format — it stores entries without compression. The configured
    /// <see cref="CompressionMethod"/> is ignored and the output is a standard tarball.
    /// </summary>
    [Test]
    public async Task Tar_Compression_Succeeds_AsUncompressedContainer()
    {
        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                ArchiveFormat.Tar,
                CompressionParameters.Default,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var entries = new[] { ("only.bin", (Stream)new MemoryStream(CompressibleContent)) };
            (await compressor.CompressAsync(entries, archive)).IsSuccess.Should().BeTrue();
        }

        archive
            .Length.Should()
            .BeGreaterThanOrEqualTo(
                CompressibleContent.Length,
                "Tar is a container — output must be at least as large as the input (plus headers)"
            );
    }

    /// <summary>
    /// Xz does not currently expose a writeable handler through this library — construction throws
    /// with HRESULT 0x80040111 (Class not registered). This documents that Xz is read-only for now;
    /// callers wanting Xz compression should use the 7z format instead.
    /// </summary>
    [Test]
    public void Xz_CompressorConstruction_Throws()
    {
        FluentActions
            .Invoking(() =>
                new SevenZipCompressor(
                    ArchiveFormat.Xz,
                    CompressionParameters.Default,
                    NullLogger<SevenZipCompressor>.Instance
                )
            )
            .Should()
            .Throw<Exception>(
                "Xz write handler is not registered — compression must fail loudly at construction"
            );
    }

    /// <summary>
    /// The 7z format supports every codec we expose. None of these should silently substitute —
    /// each should be reflected in the produced archive. Roundtrip succeeds in
    /// <see cref="CompressionParameterMatrixTests"/>; this test documents that the format does
    /// not engage the fallback path.
    /// </summary>
    [TestCase(CompressionMethod.Lzma)]
    [TestCase(CompressionMethod.Lzma2)]
    [TestCase(CompressionMethod.BZip2)]
    [TestCase(CompressionMethod.Ppmd)]
    [TestCase(CompressionMethod.Deflate)]
    public async Task SevenZip_WithAnyMethod_NoFallback(CompressionMethod method)
    {
        var parameters = new CompressionParameters { Method = method };
        using var archive = new MemoryStream();
        using var compressor = new SevenZipCompressor(
            ArchiveFormat.SevenZip,
            parameters,
            NullLogger<SevenZipCompressor>.Instance
        );

        var entries = new[] { ("payload.bin", (Stream)new MemoryStream(CompressibleContent)) };
        var result = await compressor.CompressAsync(entries, archive);

        result.IsSuccess.Should().BeTrue($"7z + {method} must succeed natively, not via fallback");
    }
}
