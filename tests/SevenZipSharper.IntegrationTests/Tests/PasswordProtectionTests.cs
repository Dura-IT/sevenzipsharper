using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests;

/// <summary>
/// Exercises <see cref="IPasswordProvider"/> — directly verifies the BSTR fix for
/// managed→native <c>out string</c> marshalling on non-Windows platforms.
/// </summary>
[TestFixture]
[TestOf(typeof(SevenZipExtractor))]
public sealed class PasswordProtectionTests
{
    private static readonly Lazy<byte[]> _archiveBytes = new(LoadFixture);

    private static byte[] LoadFixture()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName =
            "SevenZipSharper.IntegrationTests.Fixtures.password-protected.7z";
        using var stream =
            assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found."
            );
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    [Test]
    public async Task OpenAsync_WithCorrectPassword_Succeeds()
    {
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes.Value),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );

        var result = await extractor.OpenAsync(password: "TestPassword123");

        result.IsSuccess.Should().BeTrue("correct password should open the archive");
    }

    [Test]
    public async Task OpenAsync_WithWrongPassword_FailsOrExtractFails()
    {
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes.Value),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );

        // 7-Zip may succeed at Open with a wrong password (metadata is not encrypted by default)
        // but extraction will fail with DataError / CrcError.
        var openResult = await extractor.OpenAsync(password: "WrongPassword");
        if (openResult.IsFailed)
            return;

        using var output = new MemoryStream();
        var entries = (await extractor.ListEntriesAsync()).Value;
        var extractResult = await extractor.ExtractEntryAsync(entries[0], output);

        extractResult
            .IsFailed.Should()
            .BeTrue("extraction with wrong password should produce a CRC / data error");
    }

    [Test]
    public async Task ExtractAsync_WithCorrectPassword_ContentIsReadable()
    {
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes.Value),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );
        await extractor.OpenAsync(password: "TestPassword123");
        var entries = (await extractor.ListEntriesAsync()).Value;
        using var output = new MemoryStream();

        var result = await extractor.ExtractEntryAsync(entries[0], output);

        result.IsSuccess.Should().BeTrue();
        output.Length.Should().BeGreaterThan(0);
        var text = Encoding.UTF8.GetString(output.ToArray());
        text.Should().Contain("SevenZipSharper integration test secret content");
    }
}

[TestFixture]
[TestOf(typeof(SevenZipCompressor))]
public sealed class PasswordProtectedCompressionTests
{
    private static readonly byte[] Content = Encoding.UTF8.GetBytes(
        "Password-protected test content — SevenZipSharper"
    );
    private const string Password = "TestPassword123!";

    private static CompressionParameters WithPassword(
        string password,
        bool encryptHeaders = false
    ) =>
        CompressionParameters.Default with
        {
            EncryptionPassword = password,
            EncryptHeaders = encryptHeaders,
        };

    private static async Task<byte[]> CompressWithPasswordAsync(
        ArchiveFormat format,
        string password,
        bool encryptHeaders = false
    ) =>
        await IntegrationTestHelpers.BuildArchiveAsync(
            format,
            WithPassword(password, encryptHeaders),
            ("content.txt", Content)
        );

    private static SevenZipExtractor OpenExtractor(byte[] archiveBytes, ArchiveFormat format) =>
        new SevenZipExtractor(
            new MemoryStream(archiveBytes),
            format,
            NullLogger<SevenZipExtractor>.Instance
        );

    // ── Positive round-trips ─────────────────────────────────────────────────

    [TestCase(ArchiveFormat.SevenZip)]
    [TestCase(ArchiveFormat.Zip)]
    public async Task CompressAsync_WithPassword_RoundTripsAcrossFormats(ArchiveFormat format)
    {
        var archiveBytes = await CompressWithPasswordAsync(format, Password);

        using var extractor = OpenExtractor(archiveBytes, format);
        (await extractor.OpenAsync(password: Password)).IsSuccess.Should().BeTrue();
        var entries = (await extractor.ListEntriesAsync()).Value;
        using var output = new MemoryStream();
        (await extractor.ExtractEntryAsync(entries[0], output)).IsSuccess.Should().BeTrue();
        output.ToArray().Should().BeEquivalentTo(Content);
    }

    [TestCase(ArchiveFormat.SevenZip)]
    [TestCase(ArchiveFormat.Zip)]
    public async Task CompressAsync_WithPassword_ExtractWithWrongPassword_FailsAcrossFormats(
        ArchiveFormat format
    )
    {
        var archiveBytes = await CompressWithPasswordAsync(format, Password);

        using var extractor = OpenExtractor(archiveBytes, format);
        var openResult = await extractor.OpenAsync(password: "WrongPassword");
        if (openResult.IsFailed)
            return; // Open itself rejected the wrong password — valid failure mode

        var entries = (await extractor.ListEntriesAsync()).Value;
        using var output = new MemoryStream();
        var extractResult = await extractor.ExtractEntryAsync(entries[0], output);
        extractResult.IsFailed.Should().BeTrue("extracting with wrong password must fail");
    }

    [TestCase(ArchiveFormat.SevenZip)]
    [TestCase(ArchiveFormat.Zip)]
    public async Task CompressAsync_WithPassword_ResultingEntriesReportIsEncrypted(
        ArchiveFormat format
    )
    {
        var archiveBytes = await CompressWithPasswordAsync(format, Password);

        using var extractor = OpenExtractor(archiveBytes, format);
        await extractor.OpenAsync(password: Password);
        var entries = (await extractor.ListEntriesAsync()).Value;

        entries.Should().NotBeEmpty();
        entries
            .All(e => e.IsEncrypted)
            .Should()
            .BeTrue("every compressed entry must report IsEncrypted");
    }

    [TestCase(ArchiveFormat.SevenZip)]
    [TestCase(ArchiveFormat.Zip)]
    public async Task CompressAsync_WithoutPassword_ProducesUnencryptedArchive(ArchiveFormat format)
    {
        var archiveBytes = await IntegrationTestHelpers.BuildArchiveAsync(
            format,
            CompressionParameters.Default,
            ("content.txt", Content)
        );

        using var extractor = OpenExtractor(archiveBytes, format);
        await extractor.OpenAsync();
        var entries = (await extractor.ListEntriesAsync()).Value;

        entries.Should().NotBeEmpty();
        entries
            .All(e => !e.IsEncrypted)
            .Should()
            .BeTrue("entries must not be encrypted without a password");
    }

    [TestCase(ArchiveFormat.SevenZip)]
    [TestCase(ArchiveFormat.Zip)]
    public async Task CompressFilesAsync_WithPassword_RoundTrips(ArchiveFormat format)
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempPath, Content);
            var basePath = Path.GetDirectoryName(tempPath)!;

            using var archive = new MemoryStream();
            using (
                var compressor = new SevenZipCompressor(
                    format,
                    WithPassword(Password),
                    NullLogger<SevenZipCompressor>.Instance
                )
            )
            {
                var result = await compressor.CompressFilesAsync(
                    new[] { tempPath },
                    basePath,
                    archive
                );
                result.IsSuccess.Should().BeTrue();
            }

            archive.Position = 0;
            using var extractor = new SevenZipExtractor(
                archive,
                format,
                NullLogger<SevenZipExtractor>.Instance
            );
            (await extractor.OpenAsync(password: Password)).IsSuccess.Should().BeTrue();
            var entries = (await extractor.ListEntriesAsync()).Value;
            using var output = new MemoryStream();
            (await extractor.ExtractEntryAsync(entries[0], output)).IsSuccess.Should().BeTrue();
            output.ToArray().Should().BeEquivalentTo(Content);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [TestCase(ArchiveFormat.SevenZip)]
    [TestCase(ArchiveFormat.Zip)]
    public async Task CompressMultiVolumeAsync_WithPassword_RoundTrips(ArchiveFormat format)
    {
        var entries = new[] { ("content.txt", (Stream)new MemoryStream(Content)) };
        var volumeStreams = new Dictionary<int, MemoryStream>();

        Stream VolumeFactory(int i)
        {
            if (!volumeStreams.TryGetValue(i, out var ms))
                volumeStreams[i] = ms = new MemoryStream();
            return ms;
        }

        using (
            var compressor = new SevenZipCompressor(
                format,
                WithPassword(Password),
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var result = await compressor.CompressMultiVolumeAsync(
                entries,
                VolumeFactory,
                maxVolumeBytes: 10 * 1024 * 1024
            );
            result.IsSuccess.Should().BeTrue();
        }

        var archiveStream = volumeStreams[0];
        archiveStream.Position = 0;
        using var extractor = new SevenZipExtractor(
            archiveStream,
            format,
            NullLogger<SevenZipExtractor>.Instance
        );
        (await extractor.OpenAsync(password: Password)).IsSuccess.Should().BeTrue();
        var extracted = (await extractor.ListEntriesAsync()).Value;
        using var output = new MemoryStream();
        (await extractor.ExtractEntryAsync(extracted[0], output)).IsSuccess.Should().BeTrue();
        output.ToArray().Should().BeEquivalentTo(Content);
    }

    // ── 7z header encryption ─────────────────────────────────────────────────

    [Test]
    public async Task CompressAsync_SevenZip_WithEncryptHeaders_ListEntriesWithoutPassword_Fails()
    {
        var archiveBytes = await CompressWithPasswordAsync(
            ArchiveFormat.SevenZip,
            Password,
            encryptHeaders: true
        );

        using var extractor = OpenExtractor(archiveBytes, ArchiveFormat.SevenZip);
        var openResult = await extractor.OpenAsync();
        if (openResult.IsSuccess)
        {
            var listResult = await extractor.ListEntriesAsync();
            listResult
                .IsFailed.Should()
                .BeTrue("header-encrypted archive must not be listable without a password");
        }
        // else: Open itself failed — also an acceptable outcome for header encryption
    }

    [Test]
    public async Task CompressAsync_SevenZip_WithEncryptHeaders_ListEntriesWithPassword_Succeeds()
    {
        var archiveBytes = await CompressWithPasswordAsync(
            ArchiveFormat.SevenZip,
            Password,
            encryptHeaders: true
        );

        using var extractor = OpenExtractor(archiveBytes, ArchiveFormat.SevenZip);
        (await extractor.OpenAsync(password: Password)).IsSuccess.Should().BeTrue();
        var listResult = await extractor.ListEntriesAsync();
        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.Should().HaveCount(1);
    }

    // ── Negative fail-fast ───────────────────────────────────────────────────

    // Xz is omitted: the bundled natives have no Xz write handler, so the compressor ctor
    // throws before encryption validation runs. Covered by FormatFallbackBehaviorTests.
    [TestCase(ArchiveFormat.GZip)]
    [TestCase(ArchiveFormat.BZip2)]
    [TestCase(ArchiveFormat.Tar)]
    public async Task CompressAsync_WithPassword_OnUnsupportedFormat_ReturnsFailure(
        ArchiveFormat format
    )
    {
        var parameters = CompressionParameters.Default with { EncryptionPassword = Password };
        using var compressor = new SevenZipCompressor(
            format,
            parameters,
            NullLogger<SevenZipCompressor>.Instance
        );

        var result = await compressor.CompressAsync(
            new[] { ("content.txt", (Stream)new MemoryStream(Content)) },
            new MemoryStream()
        );

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("Encryption"));
    }

    [TestCase(ArchiveFormat.Zip)]
    [TestCase(ArchiveFormat.GZip)]
    [TestCase(ArchiveFormat.BZip2)]
    [TestCase(ArchiveFormat.Tar)]
    public async Task CompressAsync_WithEncryptHeaders_OnNonSevenZipFormat_ReturnsFailure(
        ArchiveFormat format
    )
    {
        var parameters = CompressionParameters.Default with
        {
            EncryptionPassword = Password,
            EncryptHeaders = true,
        };
        using var compressor = new SevenZipCompressor(
            format,
            parameters,
            NullLogger<SevenZipCompressor>.Instance
        );

        var result = await compressor.CompressAsync(
            new[] { ("content.txt", (Stream)new MemoryStream(Content)) },
            new MemoryStream()
        );

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("EncryptHeaders"));
    }

    // ── Append paths ─────────────────────────────────────────────────────────

    [Test]
    public async Task AppendAsync_ToEncryptedHeadersArchive_WithMatchingPassword_Succeeds()
    {
        var archiveBytes = await CompressWithPasswordAsync(
            ArchiveFormat.SevenZip,
            Password,
            encryptHeaders: true
        );
        var output = new MemoryStream();

        using var compressor = new SevenZipCompressor(
            ArchiveFormat.SevenZip,
            WithPassword(Password, encryptHeaders: true),
            NullLogger<SevenZipCompressor>.Instance
        );
        var result = await compressor.AppendAsync(
            new MemoryStream(archiveBytes),
            new[] { ("appended.txt", (Stream)new MemoryStream(Content)) },
            output
        );

        result
            .IsSuccess.Should()
            .BeTrue(
                "appending with the matching password to a header-encrypted archive must succeed"
            );
    }

    [Test]
    public async Task AppendAsync_ToEncryptedHeadersArchive_WithoutPassword_Fails()
    {
        var archiveBytes = await CompressWithPasswordAsync(
            ArchiveFormat.SevenZip,
            Password,
            encryptHeaders: true
        );

        using var compressor = new SevenZipCompressor(
            ArchiveFormat.SevenZip,
            CompressionParameters.Default,
            NullLogger<SevenZipCompressor>.Instance
        );
        var result = await compressor.AppendAsync(
            new MemoryStream(archiveBytes),
            new[] { ("appended.txt", (Stream)new MemoryStream(Content)) },
            new MemoryStream()
        );

        result
            .IsFailed.Should()
            .BeTrue("opening a header-encrypted archive without the password must fail");
    }

    [Test]
    public async Task AppendAsync_NewEntriesToContentEncryptedArchive_WithMatchingPassword_RoundTrips()
    {
        var existingContent = new byte[] { 1, 2, 3, 4, 5 };
        var archiveBytes = await IntegrationTestHelpers.BuildArchiveAsync(
            ArchiveFormat.SevenZip,
            WithPassword(Password),
            ("existing.bin", existingContent)
        );
        var output = new MemoryStream();

        using (
            var compressor = new SevenZipCompressor(
                ArchiveFormat.SevenZip,
                WithPassword(Password),
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            (
                await compressor.AppendAsync(
                    new MemoryStream(archiveBytes),
                    new[] { ("appended.bin", (Stream)new MemoryStream(Content)) },
                    output
                )
            )
                .IsSuccess.Should()
                .BeTrue();
        }

        output.Position = 0;
        using var extractor = new SevenZipExtractor(
            output,
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );
        (await extractor.OpenAsync(password: Password)).IsSuccess.Should().BeTrue();
        var entries = (await extractor.ListEntriesAsync()).Value;
        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.Path == "existing.bin");
        entries.Should().Contain(e => e.Path == "appended.bin");
    }

    // ── Zip AES-256 cipher assertion ─────────────────────────────────────────

    [Test]
    public async Task CompressAsync_Zip_WithPassword_UsesAes256NotZipCrypto()
    {
        var archiveBytes = await CompressWithPasswordAsync(ArchiveFormat.Zip, Password);

        // WinZip AES extra field header ID 0x9901, little-endian bytes [0x01, 0x99]
        ContainsAes256ExtraField(archiveBytes)
            .Should()
            .BeTrue("Zip encryption must use AES-256 (extra field 0x9901), not weak ZipCrypto");
    }

    // Parses the local file header extra field of the first Zip entry and looks for the
    // WinZip AES extra field (header ID 0x9901, 7 bytes of data):
    //   [vendor-version: 2B][vendor-id "AE": 2B][encryption-strength: 1B][compression-method: 2B]
    // Returns true only when the field is present AND encryption strength == 3 (AES-256).
    private static bool ContainsAes256ExtraField(byte[] zipBytes)
    {
        // Minimum Zip local file header is 30 bytes.
        if (zipBytes.Length < 30)
            return false;

        // Verify local file header signature (PK\x03\x04).
        if (
            zipBytes[0] != 0x50
            || zipBytes[1] != 0x4B
            || zipBytes[2] != 0x03
            || zipBytes[3] != 0x04
        )
        {
            return false;
        }

        var fileNameLen = BitConverter.ToUInt16(zipBytes, 26);
        var extraFieldLen = BitConverter.ToUInt16(zipBytes, 28);
        var extraStart = 30 + fileNameLen;

        if (extraStart + extraFieldLen > zipBytes.Length)
            return false;

        // Walk extra fields: each is [header-id: 2 bytes][data-size: 2 bytes][data: data-size bytes].
        var pos = extraStart;
        while (pos + 4 <= extraStart + extraFieldLen)
        {
            var headerId = BitConverter.ToUInt16(zipBytes, pos);
            var dataSize = BitConverter.ToUInt16(zipBytes, pos + 2);

            // WinZip AES extra field: header ID 0x9901, data layout (7 bytes):
            //   [vendor-version: 2B][vendor-id "AE": 2B][encryption-strength: 1B][compression-method: 2B]
            // Encryption strength is at data offset 4: 1=AES-128, 2=AES-192, 3=AES-256.
            if (headerId == 0x9901 && dataSize == 7 && pos + 4 + dataSize <= zipBytes.Length)
                return zipBytes[pos + 4 + 4] == 3;

            pos += 4 + dataSize;
        }

        return false;
    }
}
